using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Who decides what the roles may do, over HTTP: only a signed-in admin of the club (never an API key, never another club's admin),
/// a change takes effect at once, and the admins can't lock themselves out (Admin is locked, the last admin keeps the role).
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class PermissionsSecurityTests(ApiSecurityFixture app)
{
    private static string PermissionsOf(SeededClub club) => $"/api/Clubs/{club.Id}/Permissions";

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser user, string? body = null)
    {
        var cookie = await app.Login(user);
        return await app.Send(method, url, body: body, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    /// <summary>Everything that changes or shows who may do what.</summary>
    private static (HttpMethod Method, string Path, string? Body)[] Actions(SeededClub club) =>
    [
        (HttpMethod.Get, "roles", null),
        (HttpMethod.Put, "roles/Trainer", """{"permissions":["EventsManage"]}"""),
        (HttpMethod.Delete, "roles/Trainer", null),
        (HttpMethod.Delete, "roles", null),
        (HttpMethod.Get, "grants", null),
        (HttpMethod.Post, "grants", $$"""{"memberId":{{club.Member.MemberId}},"permission":"StatisticsView","effect":"Allow"}"""),
        (HttpMethod.Delete, "grants/987654321", null),
    ];

    [Fact]
    public async Task ApiKey_NeverReachesThePermissions()
    {
        var key = await app.NewKey(app.ClubA);

        using var scope = new AssertionScope();
        foreach (var (method, path, body) in Actions(app.ClubA))
        {
            var response = await app.Send(method, $"{PermissionsOf(app.ClubA)}/{path}", key.Bearer, body);
            response.Status.Should().Be(HttpStatusCode.Unauthorized, $"{method} {path}");
        }
    }

    [Fact]
    public async Task EveryoneButTheAdmin_IsForbidden()
    {
        using var scope = new AssertionScope();
        var users = new[] { app.MemberOfA, app.TreasurerOfA, app.SportsDirectorOfA, app.AuditorOfA, app.HostOfA };
        foreach (var user in users)
        {
            foreach (var (method, path, body) in Actions(app.ClubA))
            {
                var response = await Send(method, $"{PermissionsOf(app.ClubA)}/{path}", user, body);
                response.Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {path} as {user.UserId}");
            }
        }
    }

    [Fact]
    public async Task TheAdminOfAnotherClub_IsForbidden()
    {
        using var scope = new AssertionScope();
        foreach (var (method, path, body) in Actions(app.ClubA))
        {
            var response = await Send(method, $"{PermissionsOf(app.ClubA)}/{path}", app.ClubB.Admin, body);
            response.Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
        }
    }

    [Fact]
    public async Task ARevokedPermission_IsForbiddenAtOnce_AndComesBackWithTheReset()
    {
        var courtSales = $"/api/Clubs/{app.ClubA.Id}/CourtSales/purchases";
        (await Send(HttpMethod.Get, courtSales, app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.OK, "a treasurer manages court sales by default");

        try
        {
            var update = await Send(HttpMethod.Put, $"{PermissionsOf(app.ClubA)}/roles/Treasurer", app.ClubA.Admin, """{"permissions":["PaymentsManage"]}""");
            update.Status.Should().Be(HttpStatusCode.OK);

            (await Send(HttpMethod.Get, courtSales, app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await Send(HttpMethod.Delete, $"{PermissionsOf(app.ClubA)}/roles", app.ClubA.Admin);
        }

        (await Send(HttpMethod.Get, courtSales, app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task APermissionOfAnotherClub_ChangesNothingHere()
    {
        // The admin of B takes everything from the treasurers of B: the treasurer of A keeps working
        var courtSales = $"/api/Clubs/{app.ClubA.Id}/CourtSales/purchases";
        try
        {
            await Send(HttpMethod.Put, $"{PermissionsOf(app.ClubB)}/roles/Treasurer", app.ClubB.Admin, """{"permissions":[]}""");

            (await Send(HttpMethod.Get, courtSales, app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await Send(HttpMethod.Delete, $"{PermissionsOf(app.ClubB)}/roles", app.ClubB.Admin);
        }
    }

    [Fact]
    public async Task TheAdmin_CanNotBeLockedOut()
    {
        try
        {
            using var scope = new AssertionScope();

            (await Send(HttpMethod.Put, $"{PermissionsOf(app.ClubA)}/roles/Admin", app.ClubA.Admin, """{"permissions":[]}""")).Status
                .Should().Be(HttpStatusCode.PreconditionFailed, "the admin role is locked");

            foreach (var role in new[] { "Maintainer", "SportsDirector", "YouthSportsDirector", "Trainer", "Treasurer", "ClubhouseHost", "Auditor", "User" })
                await Send(HttpMethod.Put, $"{PermissionsOf(app.ClubA)}/roles/{role}", app.ClubA.Admin, """{"permissions":[]}""");

            (await Send(HttpMethod.Get, $"{PermissionsOf(app.ClubA)}/roles", app.ClubA.Admin)).Status.Should().Be(HttpStatusCode.OK, "an empty matrix does not touch the admin");
            (await Send(HttpMethod.Get, $"/api/Clubs/{app.ClubA.Id}/CourtSales/purchases", app.ClubA.Admin)).Status.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await Send(HttpMethod.Delete, $"{PermissionsOf(app.ClubA)}/roles", app.ClubA.Admin);
        }
    }

    [Fact]
    public async Task TheLastAdmin_KeepsTheAdminRole()
    {
        // Club A has more than one admin: make the others ordinary members for this test, so the seeded admin is the last one
        var otherAdmins = await app.Db(async ctx => await ctx.ClubMembers.IgnoreQueryFilters()
            .Where(m => m.ClubId == app.ClubA.Id && m.Id != app.ClubA.Admin.MemberId && m.UserRoles.Contains(Bookennis.Domain.Members.MemberRole.Admin))
            .Select(m => new { m.Id, m.UserRoles }).ToListAsync());
        await app.Db(async ctx =>
        {
            foreach (var other in otherAdmins)
                ctx.ClubMembers.IgnoreQueryFilters().Single(m => m.Id == other.Id).UpdateRoles([Bookennis.Domain.Members.MemberRole.User]);
            await ctx.SaveChangesAsync();
        });

        try
        {
            var response = await Send(HttpMethod.Patch, $"/api/Clubs/{app.ClubA.Id}/Members/{app.ClubA.Admin.MemberId}/Roles", app.ClubA.Admin, """{"roles":["User"]}""");

            response.Status.Should().Be(HttpStatusCode.PreconditionFailed);
            (await app.Db(ctx => Task.FromResult(ctx.ClubMembers.IgnoreQueryFilters().Single(m => m.Id == app.ClubA.Admin.MemberId).UserRoles)))
                .Should().Contain(Bookennis.Domain.Members.MemberRole.Admin);
        }
        finally
        {
            await app.Db(async ctx =>
            {
                foreach (var other in otherAdmins)
                    ctx.ClubMembers.IgnoreQueryFilters().Single(m => m.Id == other.Id).UpdateRoles(other.UserRoles);
                await ctx.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task AnAdmin_CanNotGetExtraPermissions_AndAnotherClubsMemberNeitherIs()
    {
        using var scope = new AssertionScope();

        var admin = await Send(HttpMethod.Post, $"{PermissionsOf(app.ClubA)}/grants", app.ClubA.Admin,
            $$"""{"memberId":{{app.ClubA.Admin.MemberId}},"permission":"StatisticsView","effect":"Deny"}""");
        admin.Status.Should().Be(HttpStatusCode.PreconditionFailed);

        var foreign = await Send(HttpMethod.Post, $"{PermissionsOf(app.ClubA)}/grants", app.ClubA.Admin,
            $$"""{"memberId":{{app.ClubB.Member.MemberId}},"permission":"StatisticsView","effect":"Allow"}""");
        foreign.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AGrantForOneCourt_DoesNotOpenTheOthers()
    {
        var grantBody = $$"""{"memberId":{{app.OtherMemberOfA.MemberId}},"permission":"CourtBlockingsManage","resourceType":"Court","resourceId":{{app.ClubA.CourtId}},"effect":"Allow"}""";
        var blockingsOf = $"/api/Clubs/{app.ClubA.Id}/CourtBlockings";
        var created = await Send(HttpMethod.Post, $"{PermissionsOf(app.ClubA)}/grants", app.ClubA.Admin, grantBody);
        created.Status.Should().Be(HttpStatusCode.OK);

        try
        {
            using var scope = new AssertionScope();
            (await Send(HttpMethod.Get, $"{blockingsOf}/manage", app.OtherMemberOfA)).Status.Should().Be(HttpStatusCode.OK, "the grant opens the door");

            // A blocking without courts, or on courts the member was not given, is not theirs to make
            var model = """{"courtIds":[987654321],"startDate":"2099-01-01","endDate":"2099-01-01","title":"x"}""";
            (await Send(HttpMethod.Post, blockingsOf, app.OtherMemberOfA, model)).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Send(HttpMethod.Post, $"{blockingsOf}/conflicts", app.OtherMemberOfA, model)).Status.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            var grants = await Send(HttpMethod.Get, $"{PermissionsOf(app.ClubA)}/grants?memberId={app.OtherMemberOfA.MemberId}", app.ClubA.Admin);
            foreach (var id in System.Text.RegularExpressions.Regex.Matches(grants.Body ?? "", "\"id\":(\\d+)").Select(m => m.Groups[1].Value))
                await Send(HttpMethod.Delete, $"{PermissionsOf(app.ClubA)}/grants/{id}", app.ClubA.Admin);
        }
    }
}
