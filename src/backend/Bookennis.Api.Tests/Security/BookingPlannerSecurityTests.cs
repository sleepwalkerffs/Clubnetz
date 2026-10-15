using System.Drawing;
using System.Net;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Booking several slots at once (<c>POST Bookings/Batch</c>, the multi-day planner) follows the rules of a single booking:
/// only who may book the play mode may use it, and only on courts and with players of the own club.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class BookingPlannerSecurityTests(ApiSecurityFixture app)
{
    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);

    private static string BatchOf(SeededClub club) => $"/api/Clubs/{club.Id}/Bookings/Batch";

    private async Task<ApiResponse> Post(string url, SeededUser user, string body)
    {
        var cookie = await app.Login(user);
        return await app.Send(HttpMethod.Post, url, body: body, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    private static string Slot(int courtId, int hour, int dayOffset = 0)
    {
        var day = Day.AddDays(dayOffset).ToString("yyyy-MM-dd");
        return $$"""{"courtId":{{courtId}},"from":"{{day}}T{{hour:00}}:00:00Z","to":"{{day}}T{{hour + 1:00}}:00:00Z"}""";
    }

    private static string Batch(int playModeId, IEnumerable<int> players, bool dryRun, params string[] slots)
        => $$"""{"slots":[{{string.Join(',', slots)}}],"timeZoneInfoId":"UTC","playModeId":{{playModeId}},"players":[{{string.Join(',', players)}}],"comment":null,"dryRun":{{(dryRun ? "true" : "false")}}}""";

    [Fact]
    public async Task MemberWithoutAnAllowedRoleOfThePlayMode_GetsForbidden()
    {
        var playModeId = await AddPlayMode(app.ClubA, [MemberRole.SportsDirector]);
        try
        {
            var before = await app.Fingerprint(app.ClubA);

            foreach (var dryRun in new[] { true, false })
            {
                var response = await Post(BatchOf(app.ClubA), app.MemberOfA, Batch(playModeId, [app.MemberOfA.MemberId], dryRun, Slot(app.ClubA.CourtId, 10), Slot(app.ClubA.CourtId, 10, 1)));
                response.Status.Should().Be(HttpStatusCode.Forbidden, $"dry run: {dryRun}");
            }

            // The same member may use the planner with a play mode of their role (the dry run answers per slot)
            var allowed = await Post(BatchOf(app.ClubA), app.MemberOfA, Batch(app.ClubA.PlayModeId, [app.MemberOfA.MemberId], true, Slot(app.ClubA.CourtId, 10)));
            allowed.Status.Should().Be(HttpStatusCode.OK);

            (await app.Fingerprint(app.ClubA)).Should().Be(before);
        }
        finally
        {
            await RemovePlayMode(app.ClubA, playModeId);
        }
    }

    [Fact]
    public async Task CourtOfAnotherClub_IsNotFoundAndNothingIsBooked()
    {
        var beforeA = await app.Fingerprint(app.ClubA);
        var beforeB = await app.Fingerprint(app.ClubB);

        foreach (var user in new[] { app.MemberOfA, app.ClubA.Admin })
        {
            foreach (var dryRun in new[] { true, false })
            {
                // A valid slot on the own court together with one on the court of club B
                var mixed = await Post(BatchOf(app.ClubA), user, Batch(app.ClubA.PlayModeId, [user.MemberId], dryRun, Slot(app.ClubA.CourtId, 15), Slot(app.ClubB.CourtId, 15)));
                mixed.Status.Should().Be(HttpStatusCode.NotFound, $"{user.Email}, dry run: {dryRun}");
                mixed.Body.Should().NotContain(ApiSecurityFixture.Canary);

                var onlyB = await Post(BatchOf(app.ClubA), user, Batch(app.ClubA.PlayModeId, [user.MemberId], dryRun, Slot(app.ClubB.CourtId, 16)));
                onlyB.Status.Should().Be(HttpStatusCode.NotFound, $"{user.Email}, dry run: {dryRun}");
            }
        }

        // Players of club B can't be booked through club A either
        var foreignPlayer = await Post(BatchOf(app.ClubA), app.ClubA.Admin, Batch(app.ClubA.PlayModeId, [app.ClubA.Admin.MemberId, app.ClubB.Member.MemberId], false, Slot(app.ClubA.CourtId, 17)));
        foreignPlayer.Status.Should().Be(HttpStatusCode.NotFound);

        (await app.Fingerprint(app.ClubA)).Should().Be(beforeA);
        (await app.Fingerprint(app.ClubB)).Should().Be(beforeB);
    }

    [Fact]
    public async Task PlayModeOfAnotherClub_IsForbiddenOrNotFoundAndNothingIsBooked()
    {
        var beforeA = await app.Fingerprint(app.ClubA);
        var beforeB = await app.Fingerprint(app.ClubB);
        var a = app.ClubA;
        var single = $$"""
            {"courtId":{{a.CourtId}},"from":"{{Day:yyyy-MM-dd}}T10:00:00Z","to":"{{Day:yyyy-MM-dd}}T11:00:00Z","timeZoneInfoId":"UTC","playModeId":{{app.ClubB.PlayModeId}},"players":[{{app.MemberOfA.MemberId}}]}
            """;

        // A plain member fails the play mode authorization (it used to end in a 500)
        (await Post(BatchOf(a), app.MemberOfA, Batch(app.ClubB.PlayModeId, [app.MemberOfA.MemberId], false, Slot(a.CourtId, 10)))).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Post($"/api/Clubs/{a.Id}/Bookings/BookCourt", app.MemberOfA, single)).Status.Should().Be(HttpStatusCode.Forbidden);

        // An admin passes every play mode of the own club, but the play mode of club B is not found
        (await Post(BatchOf(a), a.Admin, Batch(app.ClubB.PlayModeId, [a.Admin.MemberId], false, Slot(a.CourtId, 10)))).Status.Should().Be(HttpStatusCode.NotFound);

        (await app.Fingerprint(app.ClubA)).Should().Be(beforeA);
        (await app.Fingerprint(app.ClubB)).Should().Be(beforeB);
    }

    [Fact]
    public async Task SportsDirectorPlansATournament_BookingsAreSavedAndTheOverbookedBookingIsRemoved()
    {
        var club = app.ClubA;
        var admin = club.Admin;
        var playModeId = await AddPlayMode(club, [MemberRole.Admin], canOverbook: true);
        var (memberSeasonId, overbookedId) = await app.Db(async context =>
        {
            var memberSeason = new MemberSeason(admin.MemberId, club.SeasonId);
            var day = Day.ToDateTime(new TimeOnly(12, 0));
            var overbooked = new Domain.Bookings.Booking(club.Id, club.CourtId, club.PlayModeId,
                new Global.Intervals.DateTimeOffsetInterval(new DateTimeOffset(day, TimeSpan.Zero), new DateTimeOffset(day.AddHours(1), TimeSpan.Zero)), "UTC", [admin.MemberId]);
            context.AddRange(memberSeason, overbooked);
            await context.SaveChangesAsync();
            return (memberSeason.Id, overbooked.Id);
        });

        List<int> created = [];
        try
        {
            var beforeB = await app.Fingerprint(app.ClubB);

            var dryRun = await Post(BatchOf(club), admin, Batch(playModeId, [admin.MemberId], true, Slot(club.CourtId, 12), Slot(club.CourtId, 12, 1)));
            dryRun.Status.Should().Be(HttpStatusCode.OK, dryRun.Body);
            dryRun.Body.Should().Contain($"\"bookingEntryId\":{overbookedId}").And.Contain("\"createdBookingIds\":[]");

            var response = await Post(BatchOf(club), admin, Batch(playModeId, [admin.MemberId], false, Slot(club.CourtId, 12), Slot(club.CourtId, 12, 1)));
            response.Status.Should().Be(HttpStatusCode.OK, response.Body);

            var bookings = await app.Db(context => context.Bookings.IgnoreQueryFilters().Where(b => b.ClubId == club.Id && b.PlayModeId == playModeId).Select(b => b.Id).ToListAsync());
            created = bookings;
            bookings.Should().HaveCount(2);
            (await app.Db(context => context.Bookings.IgnoreQueryFilters().AnyAsync(b => b.Id == overbookedId))).Should().BeFalse("the overbooking play mode removes the booking");
            (await app.Fingerprint(app.ClubB)).Should().Be(beforeB);
        }
        finally
        {
            await app.Db(async context =>
            {
                await context.Bookings.IgnoreQueryFilters().Where(b => created.Contains(b.Id) || b.Id == overbookedId).ExecuteDeleteAsync();
                await context.MemberSeasons.Where(ms => ms.Id == memberSeasonId).ExecuteDeleteAsync();
            });
            await RemovePlayMode(club, playModeId);
        }
    }

    private Task<int> AddPlayMode(SeededClub club, MemberRole[] allowedRoles, bool canOverbook = true) => app.Db(async context =>
    {
        var entity = await context.Clubs.IgnoreQueryFilters().Include(c => c.PlayModes).SingleAsync(c => c.Id == club.Id);
        var name = $"Planner {Guid.NewGuid():N}";
        entity.AddPlayMode(new Club.PlayModeDto(allowedRoles, Color.Red, null, false, name, null, canOverbook, true));
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
