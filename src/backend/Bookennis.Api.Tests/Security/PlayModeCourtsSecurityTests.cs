using System.Drawing;
using System.Net;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Play modes limited to some courts: only the club admin sets the courts and asks for the conflicting bookings, only courts of the
/// own club can be chosen, and the limit holds for every booking (here the planner, which uses the rules of a single booking).
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class PlayModeCourtsSecurityTests(ApiSecurityFixture app)
{
    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser user, string body)
    {
        var cookie = await app.Login(user);
        return await app.Send(method, url, body: body, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    private static string PlayModeBody(string name, params int[] courtIds)
        => $$"""{"name":"{{name}}","allowedRoles":[0],"color":-65536,"fixedPlayerCount":null,"isChargingBookingSubscription":false,"fixedDuration":null,"canOverbook":false,"commentAllowed":false,"courtIds":[{{string.Join(',', courtIds)}}]}""";

    [Fact]
    public async Task MemberWithoutAdminRole_CannotLimitAPlayModeOrSeeItsConflicts()
    {
        var before = await app.Fingerprint(app.ClubA);
        var url = $"/api/Clubs/{app.ClubA.Id}/PlayModes/{app.ClubA.PlayModeId}";

        var update = await Send(HttpMethod.Put, url, app.MemberOfA, PlayModeBody("Taken over", app.ClubA.CourtId));
        var conflicts = await Send(HttpMethod.Post, $"{url}/court-conflicts", app.MemberOfA, $$"""{"courtIds":[{{app.ClubA.CourtId}}]}""");

        update.Status.Should().Be(HttpStatusCode.Forbidden);
        conflicts.Status.Should().Be(HttpStatusCode.Forbidden);
        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task AdminOfAnotherClub_CannotSeeTheConflictsOfThePlayMode()
    {
        var response = await Send(HttpMethod.Post, $"/api/Clubs/{app.ClubA.Id}/PlayModes/{app.ClubA.PlayModeId}/court-conflicts", app.ClubB.Admin, $$"""{"courtIds":[{{app.ClubA.CourtId}}]}""");

        response.Status.Should().Be(HttpStatusCode.Forbidden);
        response.Body.Should().NotContain(ApiSecurityFixture.Canary);
    }

    [Fact]
    public async Task Admin_CannotLimitAPlayModeToACourtOfAnotherClub()
    {
        var playModeId = await AddPlayMode(app.ClubA);
        try
        {
            var before = await app.Fingerprint(app.ClubA);

            var response = await Send(HttpMethod.Put, $"/api/Clubs/{app.ClubA.Id}/PlayModes/{playModeId}", app.ClubA.Admin, PlayModeBody("Foreign", app.ClubA.CourtId, app.ClubB.CourtId));

            response.Status.Should().Be(HttpStatusCode.NotFound);
            (await app.Fingerprint(app.ClubA)).Should().Be(before);
        }
        finally
        {
            await RemovePlayMode(app.ClubA, playModeId);
        }
    }

    [Fact]
    public async Task PlayModeLimitedToAnotherCourt_CannotBeBookedOnThisCourt()
    {
        var playModeId = await AddPlayMode(app.ClubA);
        var otherCourtId = await app.Db(async context =>
        {
            var court = new Court(app.ClubA.Id, $"Hall {Guid.NewGuid():N}", "H", 99);
            context.Courts.Add(court);
            await context.SaveChangesAsync();
            return court.Id;
        });
        try
        {
            var limit = await Send(HttpMethod.Put, $"/api/Clubs/{app.ClubA.Id}/PlayModes/{playModeId}", app.ClubA.Admin, PlayModeBody("Hall only", otherCourtId));
            limit.Status.Should().Be(HttpStatusCode.OK);

            var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(21).ToString("yyyy-MM-dd");
            var batch = $$"""{"slots":[{"courtId":{{app.ClubA.CourtId}},"from":"{{day}}T10:00:00Z","to":"{{day}}T11:00:00Z"}],"timeZoneInfoId":"UTC","playModeId":{{playModeId}},"players":[{{app.ClubA.Admin.MemberId}}],"comment":null,"dryRun":true}""";
            var response = await Send(HttpMethod.Post, $"/api/Clubs/{app.ClubA.Id}/Bookings/Batch", app.ClubA.Admin, batch);

            response.Body.Should().Contain("PlayModeNotAllowedOnCourt", "the limit holds for admins as well");
        }
        finally
        {
            await RemovePlayMode(app.ClubA, playModeId);
            await app.Db(context => context.Courts.IgnoreQueryFilters().Where(c => c.Id == otherCourtId).ExecuteDeleteAsync());
        }
    }

    private Task<int> AddPlayMode(SeededClub club) => app.Db(async context =>
    {
        var entity = await context.Clubs.IgnoreQueryFilters().Include(c => c.PlayModes).SingleAsync(c => c.Id == club.Id);
        var name = $"Courts {Guid.NewGuid():N}";
        entity.AddPlayMode(new Club.PlayModeDto([MemberRole.User], Color.Red, null, false, name, null, false, false));
        await context.SaveChangesAsync();
        return entity.PlayModes.Single(p => p.Name == name).Id;
    });

    private Task RemovePlayMode(SeededClub club, int playModeId) => app.Db(async context =>
    {
        var entity = await context.Clubs.IgnoreQueryFilters().Include(c => c.PlayModes).SingleAsync(c => c.Id == club.Id);
        entity.RemovePlayMode(playModeId);
        await context.SaveChangesAsync();
    });
}
