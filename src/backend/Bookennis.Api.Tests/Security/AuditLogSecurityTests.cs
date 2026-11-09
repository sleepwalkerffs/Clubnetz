using System.Net;
using System.Text;
using System.Text.Json;
using Bookennis.Api.Business.Account;
using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Admin.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// The audit log through the running app (see <see cref="ApiSecurityFixture"/>): failed sign-ins and refused requests are recorded with
/// who and from where, and an impersonated session names the administrator behind it and can only go back to that administrator.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class AuditLogSecurityTests(ApiSecurityFixture app)
{
    [Fact]
    public async Task FailedSignIn_IsRecorded_WithTheAccountAndTheAddress()
    {
        var user = await app.NewAdmin(app.ClubA);

        var response = await app.Send(HttpMethod.Post, "/api/Account/Login", body: $$"""{"email":"{{user.Email}}","password":"wrong-Password-1","rememberMe":false}""",
            configure: r => r.Headers.Add("X-Forwarded-For", "10.99.1.1"));
        response.Status.Should().Be(HttpStatusCode.PreconditionFailed);

        var e = await app.Db(ctx => ctx.AuditEvents.SingleAsync(x => x.Action == nameof(LoginUser) && x.SubjectUserId == user.UserId));
        e.Kind.Should().Be(AuditEventKind.Security);
        e.Outcome.Should().Be(AuditOutcome.Failed);
        e.ErrorCode.Should().Be(nameof(LoginUser.ErrorCode.InvalidCredentials));
        e.IpAddress.Should().Be("10.99.1.1");
        e.ActorUserId.Should().BeNull("nobody was signed in");
    }

    [Fact]
    public async Task RefusedRequest_IsRecordedAsAccessDenied()
    {
        var response = await app.Send(HttpMethod.Get, "/api/Admin/Users", configure: Cookie(await app.Login(app.MemberOfA)));
        response.Status.Should().Be(HttpStatusCode.Forbidden);

        var e = await app.Db(ctx => ctx.AuditEvents
                                       .Where(x => x.Action == AccessDeniedAudit.Action && x.ActorUserId == app.MemberOfA.UserId && x.Request == "GET /api/Admin/Users")
                                       .SingleAsync());
        e.Outcome.Should().Be(AuditOutcome.Denied);
        e.Kind.Should().Be(AuditEventKind.Security);
    }

    [Fact]
    public async Task ReadOnlyKeyThatTriesToWrite_IsRecordedAsAccessDenied()
    {
        var key = await app.NewKey(app.ClubA, app.ClubA.Admin.UserId, isReadOnly: true);

        var response = await app.Send(HttpMethod.Post, $"/api/Clubs/{app.ClubA.Id}/Bookings/BookCourt", key.Bearer, body: "{}");
        response.Status.Should().Be(HttpStatusCode.Forbidden);

        var e = await app.Db(ctx => ctx.AuditEvents.SingleAsync(x => x.Action == AccessDeniedAudit.Action && x.ApiKeyId == key.Id));
        e.Outcome.Should().Be(AuditOutcome.Denied);
        e.ErrorCode.Should().Be(ClubApiKeyReadOnlyFilter.ErrorCode);
        e.ClubId.Should().Be(app.ClubA.Id);
        e.ActorName.Should().NotBeNullOrWhiteSpace("the key acts as its creator, and the log names them");
    }

    [Fact]
    public async Task ImpersonatedSession_NamesTheAdministrator_AndGoesBackToThem()
    {
        var target = await app.NewAdmin(app.ClubA);
        var administrator = app.ClubA.Admin;

        var start = await app.Send(HttpMethod.Post, $"/api/Admin/Users/{target.UserId}/Impersonate", configure: Cookie(await app.Login(administrator)));
        start.Status.Should().Be(HttpStatusCode.OK);
        var impersonated = SessionCookie(start);

        var asTarget = await User(impersonated);
        asTarget.GetProperty("id").GetInt32().Should().Be(target.UserId);
        asTarget.GetProperty("impersonatorName").GetString().Should().NotBeNullOrEmpty();

        var stop = await app.Send(HttpMethod.Post, "/api/Account/Impersonation/Stop", configure: Cookie(impersonated));
        stop.Status.Should().Be(HttpStatusCode.OK);

        var back = await User(SessionCookie(stop));
        back.GetProperty("id").GetInt32().Should().Be(administrator.UserId);
        back.GetProperty("impersonatorName").ValueKind.Should().Be(JsonValueKind.Null);

        var started = await app.Db(ctx => ctx.AuditEvents.SingleAsync(x => x.Action == nameof(ImpersonateUser) && x.SubjectUserId == target.UserId));
        started.ActorUserId.Should().Be(administrator.UserId);
        started.Outcome.Should().Be(AuditOutcome.Succeeded);
        started.SubjectName.Should().NotBeNullOrWhiteSpace("the impersonated user is named, not only their id");

        var stopped = await app.Db(ctx => ctx.AuditEvents.SingleAsync(x => x.Action == nameof(StopImpersonation) && x.ActorUserId == target.UserId));
        stopped.ImpersonatorUserId.Should().Be(administrator.UserId, "what happens in the impersonated session is the administrator's doing");
        stopped.SubjectUserId.Should().Be(administrator.UserId);
    }

    [Fact]
    public async Task StopImpersonation_DoesNotSignAnybodyInFromANormalSession()
    {
        var response = await app.Send(HttpMethod.Post, "/api/Account/Impersonation/Stop", configure: Cookie(await app.Login(app.OtherMemberOfA)));

        response.Status.Should().Be(HttpStatusCode.PreconditionFailed);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/Admin/Audit/Events")]
    [InlineData("/api/Admin/Audit/Events/1")]
    [InlineData("/api/Admin/Audit/Options")]
    [InlineData("/api/Admin/Audit/Events/Counts?utcOffsetMinutes=60")]
    [InlineData("/api/Admin/Audit/Events/Export")]
    public async Task TheLog_IsOnlyForTheApplicationAdministrator(string url)
    {
        // Club B's admin manages a club but is no administrator of the installation; the key is one of the application administrator
        (await app.Send(HttpMethod.Get, url, configure: Cookie(await app.Login(app.ClubB.Admin)))).Status.Should().Be(HttpStatusCode.Forbidden);
        (await app.Send(HttpMethod.Get, url, configure: Cookie(await app.Login(app.MemberOfA)))).Status.Should().Be(HttpStatusCode.Forbidden);
        var key = await app.NewKey(app.ClubA, app.ClubA.Admin.UserId);
        (await app.Send(HttpMethod.Get, url, key.Bearer)).Status.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await app.Send(HttpMethod.Get, url)).Status.Should().Be(HttpStatusCode.Unauthorized);

        var asAdministrator = await app.Send(HttpMethod.Get, url, configure: Cookie(await app.Login(app.ClubA.Admin)));
        asAdministrator.Status.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    private static Action<HttpRequestMessage> Cookie(string cookie) => request => request.Headers.Add("Cookie", cookie);

    private static string SessionCookie(ApiResponse response)
        => string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]));

    private async Task<JsonElement> User(string cookie)
    {
        var response = await app.Send(HttpMethod.Get, "/api/Account/User", configure: Cookie(cookie));
        response.Status.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(response.Body).RootElement;
    }
}
