using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class GetMemberOverviewTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMemberOverview_ReturnsBookingsAndSeasons()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var memberId = Query(ctx => ctx.TestData().Member1.Id);
        SetTenantId(clubId);

        var (lastSeasonId, thisSeasonId) = await QueryAsync(async ctx =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var lastSeason = new Season(clubId, new DateOnlyInterval(today.AddYears(-1).AddDays(-30), today.AddYears(-1).AddDays(30)));
            var thisSeason = new Season(clubId, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.AddRange(lastSeason, thisSeason);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(memberId, thisSeason.Id));

            var court = ctx.TestData().Court1;
            var playMode = ctx.TestData().Club.PlayModes[0];

            // Two played bookings this season (1 h and 1.5 h), one last season and one upcoming booking
            var played1 = DateTimeOffset.UtcNow.AddDays(-5);
            var played2 = DateTimeOffset.UtcNow.AddDays(-2);
            var lastYear = DateTimeOffset.UtcNow.AddYears(-1);
            var upcoming = DateTimeOffset.UtcNow.AddDays(3);
            ctx.AddRange(
                new Booking(clubId, court.Id, playMode.Id, new DateTimeOffsetInterval(played1, played1.AddHours(1)), TimeZoneInfo.Utc.Id, [memberId]),
                new Booking(clubId, court.Id, playMode.Id, new DateTimeOffsetInterval(played2, played2.AddHours(1.5)), TimeZoneInfo.Utc.Id, [memberId]),
                new Booking(clubId, court.Id, playMode.Id, new DateTimeOffsetInterval(lastYear, lastYear.AddHours(1)), TimeZoneInfo.Utc.Id, [memberId]),
                new Booking(clubId, court.Id, playMode.Id, new DateTimeOffsetInterval(upcoming, upcoming.AddHours(1)), TimeZoneInfo.Utc.Id, [memberId]));
            await ctx.SaveChangesAsync();

            return (lastSeason.Id, thisSeason.Id);
        });

        var result = await SendAsync(new GetMemberOverview(clubId, memberId));

        result.TotalBookings.Should().Be(3);
        result.TotalHours.Should().Be(3.5);
        result.UpcomingBookings.Should().Be(1);
        result.LastPlayed.Should().NotBeNull();
        result.Seasons.Select(s => s.SeasonId).Should().Equal(thisSeasonId, lastSeasonId);

        var thisSeason = result.Seasons[0];
        thisSeason.IsEnrolled.Should().BeTrue();
        thisSeason.Bookings.Should().Be(2);
        thisSeason.Hours.Should().Be(2.5);

        var lastSeason = result.Seasons[1];
        lastSeason.IsEnrolled.Should().BeFalse();
        lastSeason.Bookings.Should().Be(1);
    }

    [Fact]
    public async Task GetMemberOverview_MemberOfOtherClub_Throws()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        var act = () => SendAsync(new GetMemberOverview(-1, memberId));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
