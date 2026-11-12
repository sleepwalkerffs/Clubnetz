using Bookennis.Api.Business.Admin;
using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

/// <summary>Limiting a play mode to some courts (<see cref="PlayMode.CourtIds"/>).</summary>
public class PlayModeCourtsTests(TestFixture fixture) : TestBase(fixture)
{
    private const int Red = unchecked((int)0xFFFF0000);
    private const int UnknownCourtId = 987_654_321;

    [Fact]
    public async Task UpdatePlayMode_WithCourts_StoresThemAndGetPlayModesReturnsThem()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        await SendAsync(Update(playModeId, [TestDataSeed.Court2Id, TestDataSeed.Court1Id, TestDataSeed.Court2Id]));

        var result = await SendAsync(new GetPlayModes());
        result.PlayModes.Single(p => p.Id == playModeId).CourtIds.Should().Equal(TestDataSeed.Court1Id, TestDataSeed.Court2Id);
    }

    [Fact]
    public async Task UpdatePlayMode_WithoutCourts_AllowsEveryCourtAgain()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        await SendAsync(Update(playModeId, [TestDataSeed.Court1Id]));

        await SendAsync(Update(playModeId, []));

        var result = await SendAsync(new GetPlayModes());
        var playMode = result.PlayModes.Single(p => p.Id == playModeId);
        playMode.CourtIds.Should().BeEmpty();
        playMode.IsAllowedOnCourt(TestDataSeed.Court2Id).Should().BeTrue();
    }

    [Fact]
    public async Task AddPlayMode_WithCourts_StoresThem()
    {
        await SendAsync(new AddPlayMode("Training", [MemberRole.User], Red, null, false, null, false, false, CourtIds: [TestDataSeed.Court2Id]));

        var playMode = await QueryAsync(ctx => ctx.PlayModes.SingleAsync(p => p.Name == "Training"));
        playMode.CourtIds.Should().Equal(TestDataSeed.Court2Id);
    }

    [Fact]
    public async Task UpdatePlayMode_UnknownCourt_IsRejected()
    {
        // Courts of another club are checked over HTTP (PlayModeCourtsSecurityTests), where the tenant of the route applies
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        var act = () => SendAsync(Update(playModeId, [TestDataSeed.Court1Id, UnknownCourtId]));

        await act.Should().ThrowAsync<EntityNotFoundException>();
        var playMode = await QueryAsync(ctx => ctx.PlayModes.SingleAsync(p => p.Id == playModeId));
        playMode.CourtIds.Should().BeEmpty();
    }

    [Fact]
    public async Task AddPlayMode_UnknownCourt_IsRejected()
    {

        var act = () => SendAsync(new AddPlayMode("Foreign", [MemberRole.User], Red, null, false, null, false, false, CourtIds: [UnknownCourtId]));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task GetPlayModeCourtConflicts_CountsOnlyUpcomingBookingsOfThePlayModeOnOtherCourts()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        await SendAsync(new AddPlayMode("Training", [MemberRole.User], Red, null, false, null, false, false));
        var otherPlayModeId = await QueryAsync(ctx => ctx.PlayModes.Where(p => p.Name == "Training").Select(p => p.Id).SingleAsync());
        var tomorrow = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);
        await QueryAsync(async ctx =>
        {
            // On the court that is kept, on the other court (twice), in the past and of another play mode
            ctx.Add(Booking(TestDataSeed.Court1Id, playModeId, tomorrow.AddHours(8)));
            ctx.Add(Booking(TestDataSeed.Court2Id, playModeId, tomorrow.AddHours(9)));
            ctx.Add(Booking(TestDataSeed.Court2Id, playModeId, tomorrow.AddHours(11)));
            ctx.Add(Booking(TestDataSeed.Court2Id, playModeId, tomorrow.AddDays(-3).AddHours(9)));
            ctx.Add(Booking(TestDataSeed.Court2Id, otherPlayModeId, tomorrow.AddHours(13)));
            await ctx.SaveChangesAsync();
        });

        var limited = await SendAsync(new GetPlayModeCourtConflicts(TestDataSeed.ClubId, playModeId, [TestDataSeed.Court1Id]));
        var everyCourt = await SendAsync(new GetPlayModeCourtConflicts(TestDataSeed.ClubId, playModeId, []));

        limited.UpcomingBookingsOnOtherCourts.Should().Be(2);
        everyCourt.UpcomingBookingsOnOtherCourts.Should().Be(0);
    }

    [Fact]
    public async Task GetPlayModeCourtConflicts_DoesNotCountBookingsOfAnotherClub()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        var result = await SendAsync(new GetPlayModeCourtConflicts(TestDataSeed.ClubId + 1, playModeId, [TestDataSeed.Court1Id]));

        result.UpcomingBookingsOnOtherCourts.Should().Be(0);
    }

    [Fact]
    public async Task AdminDeleteCourt_RemovesTheCourtFromLimitedPlayModes()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var courtId = await QueryAsync(async ctx =>
        {
            var court = new Court(TestDataSeed.ClubId, "Hall", "H", 100);
            ctx.Courts.Add(court);
            await ctx.SaveChangesAsync();
            return court.Id;
        });
        await SendAsync(Update(playModeId, [TestDataSeed.Court1Id, courtId]));

        await SendAsync(new AdminDeleteCourt(TestDataSeed.ClubId, courtId));

        var playMode = await QueryAsync(ctx => ctx.PlayModes.SingleAsync(p => p.Id == playModeId));
        playMode.CourtIds.Should().Equal(TestDataSeed.Court1Id);
    }

    private static UpdatePlayMode Update(int playModeId, int[] courtIds)
        => new(playModeId, "Single", [MemberRole.User], Red, 2, true, null, false, false, CourtIds: courtIds);

    private static Booking Booking(int courtId, int playModeId, DateTimeOffset from)
        => new(TestDataSeed.ClubId, courtId, playModeId, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [TestDataSeed.Member1Id]);
}
