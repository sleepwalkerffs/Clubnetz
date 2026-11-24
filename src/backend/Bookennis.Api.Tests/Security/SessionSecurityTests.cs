using System.Net;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// The security stamp ends other sessions: after a password change or with a deleted account a cookie is not accepted any more.
/// The test app checks the stamp on every request (the app every 30 minutes, see <c>ApiSecurityFixture</c>).
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class SessionSecurityTests(ApiSecurityFixture app)
{
    [Fact]
    public async Task UnchangedSession_StaysSignedIn()
    {
        var user = await app.NewAdmin(app.ClubA);
        var cookie = await app.SignInAgain(user);

        for (var i = 0; i < 3; i++)
            (await app.Send(HttpMethod.Get, "/api/Profile", configure: As(cookie, "10.91.0.1"))).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangingThePassword_EndsTheOtherSessions()
    {
        var user = await app.NewAdmin(app.ClubA);
        var laptop = await app.SignInAgain(user);
        var phone = await app.SignInAgain(user);

        var change = await app.Send(HttpMethod.Post, "/api/Profile/ChangePassword",
            body: $$"""{"currentPassword":"{{ApiSecurityFixture.Password}}","newPassword":"Sec-Test-5678!"}""", configure: As(laptop, "10.91.0.2"));
        change.Status.Should().Be(HttpStatusCode.OK);

        (await app.Send(HttpMethod.Get, "/api/Profile", configure: As(phone, "10.91.0.2"))).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletingTheAccount_EndsEverySession()
    {
        var user = await app.NewAdmin(app.ClubA);
        var laptop = await app.SignInAgain(user);
        var phone = await app.SignInAgain(user);

        var delete = await app.Send(HttpMethod.Post, "/api/Profile/DeleteAccount",
            body: $$"""{"currentPassword":"{{ApiSecurityFixture.Password}}"}""", configure: As(laptop, "10.91.0.3"));
        delete.Status.Should().Be(HttpStatusCode.OK);

        (await app.Send(HttpMethod.Get, "/api/Profile", configure: As(phone, "10.91.0.3"))).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    // An address of its own: the sign-in and password endpoints are limited per address
    private static Action<HttpRequestMessage> As(string cookie, string address) => request =>
    {
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("X-Forwarded-For", address);
    };
}
