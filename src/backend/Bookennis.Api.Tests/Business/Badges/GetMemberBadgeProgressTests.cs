using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class GetMemberBadgeProgressTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMemberBadgeProgress_NoActiveSeason_ReturnsZeroProgress()
    {
        var (clubId, memberId) = Query(ctx =>
        {
            var testData = ctx.TestData();
            return (testData.Club.Id, testData.Member1.Id);
        });

        var result = await SendAsync(new GetMemberBadgeProgress(memberId, clubId));

        result.MatchCount.Should().Be(0);
        result.CurrentBadgeTierId.Should().BeNull();
        result.CurrentBadgeName.Should().BeNull();
        result.CurrentBadgeLevel.Should().BeNull();
        result.NextBadgeTierId.Should().BeNull();
        result.NextBadgeName.Should().BeNull();
        result.NextBadgeMatchesRequired.Should().BeNull();
        result.EarnedBadges.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberBadgeProgress_ActiveSeasonWithBookings_ReturnsProgressAndAwardsBadges()
    {
        var seeded = await SeedActiveSeasonProgressData(bookingCount: 3);

        var result = await SendAsync(new GetMemberBadgeProgress(seeded.MemberId, seeded.ClubId));

        result.MatchCount.Should().Be(3);
        result.CurrentBadgeTierId.Should().Be(seeded.SilverTierId);
        result.CurrentBadgeName.Should().Be("Silver");
        result.CurrentBadgeLevel.Should().Be(2);
        result.NextBadgeTierId.Should().Be(seeded.GoldTierId);
        result.NextBadgeName.Should().Be("Gold");
        result.NextBadgeMatchesRequired.Should().Be(5);
        result.EarnedBadges.Select(b => b.BadgeTierId).Should().Equal(seeded.BronzeTierId, seeded.SilverTierId);

        var persistedBadges = await QueryAsync(ctx => ctx.MemberBadges.Where(b => b.MemberId == seeded.MemberId).ToListAsync());
        persistedBadges.Select(b => b.BadgeTierId).Should().BeEquivalentTo([seeded.BronzeTierId, seeded.SilverTierId]);
    }

    [Fact]
    public async Task GetMemberBadgeProgress_RepeatedCalls_DoNotCreateDuplicateBadges()
    {
        var seeded = await SeedActiveSeasonProgressData(bookingCount: 3);

        await SendAsync(new GetMemberBadgeProgress(seeded.MemberId, seeded.ClubId));
        var secondResult = await SendAsync(new GetMemberBadgeProgress(seeded.MemberId, seeded.ClubId));

        var persistedBadges = await QueryAsync(ctx => ctx.MemberBadges.Where(b => b.MemberId == seeded.MemberId).ToListAsync());

        secondResult.EarnedBadges.Should().HaveCount(2);
        persistedBadges.Should().HaveCount(2);
        persistedBadges.Select(b => b.BadgeTierId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetMemberBadgeProgress_TiersOfEverySport_OneTrackWithTheSameValues()
    {
        var seeded = await SeedActiveSeasonProgressData(bookingCount: 3);

        var result = await SendAsync(new GetMemberBadgeProgress(seeded.MemberId, seeded.ClubId));

        var track = result.Tracks.Should().ContainSingle().Subject;
        track.Sport.Should().BeNull();
        track.MatchCount.Should().Be(3);
        track.CurrentBadgeTierId.Should().Be(seeded.SilverTierId);
        track.NextBadgeTierId.Should().Be(seeded.GoldTierId);
        track.NextBadgeMatchesRequired.Should().Be(5);
    }

    [Fact]
    public async Task GetMemberBadgeProgress_PadelTrack_CountsOnlyPadelMatches()
    {
        // 3 tennis matches on court 1, 1 padel match on court 2
        var seeded = await SeedActiveSeasonProgressData(bookingCount: 3);
        var (padelBronzeId, padelSilverId) = await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            testData.Court2.SetSport(Sport.Padel);
            var seasonId = await ctx.BadgeTiers.Where(t => t.Id == seeded.BronzeTierId).Select(t => t.SeasonId).SingleAsync();
            var padelBronze = new BadgeTier(seeded.ClubId, seasonId, 1, "Padel Bronze", "", 1, 1, Sport.Padel);
            var padelSilver = new BadgeTier(seeded.ClubId, seasonId, 2, "Padel Silver", "", 2, 2, Sport.Padel);
            ctx.AddRange(padelBronze, padelSilver);
            var from = DateTimeOffset.UtcNow.AddDays(-1);
            ctx.Add(new Booking(seeded.ClubId, TestDataSeed.Court2Id, testData.Club.PlayModes[0].Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [seeded.MemberId]));
            await ctx.SaveChangesAsync();
            return (padelBronze.Id, padelSilver.Id);
        });

        var result = await SendAsync(new GetMemberBadgeProgress(seeded.MemberId, seeded.ClubId));

        // The result itself stays the track of every sport: 4 matches
        result.MatchCount.Should().Be(4);
        result.CurrentBadgeTierId.Should().Be(seeded.SilverTierId);
        result.Tracks.Select(t => t.Sport).Should().Equal(null, Bookennis.Shared.Controller.Shared.Sport.Padel);

        var padel = result.Tracks[1];
        padel.MatchCount.Should().Be(1);
        padel.CurrentBadgeTierId.Should().Be(padelBronzeId);
        padel.NextBadgeTierId.Should().Be(padelSilverId);
        padel.NextBadgeMatchesRequired.Should().Be(2);

        result.EarnedBadges.Should().ContainSingle(b => b.BadgeTierId == padelBronzeId)
            .Which.Sport.Should().Be(Bookennis.Shared.Controller.Shared.Sport.Padel);
        result.EarnedBadges.Select(b => b.BadgeTierId).Should().NotContain(padelSilverId);
    }

    [Fact]
    public async Task GetMemberBadgeProgress_OnlyTiersWithSport_FirstTrackIsTheResult()
    {
        var (clubId, memberId, tennisTierId) = await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            testData.Court2.SetSport(Sport.Padel);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Season(testData.Club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var padel = new BadgeTier(testData.Club.Id, season.Id, 1, "Padel", "", 1, 1, Sport.Padel);
            var tennis = new BadgeTier(testData.Club.Id, season.Id, 1, "Tennis", "", 1, 1, Sport.Tennis);
            ctx.AddRange(padel, tennis);
            var from = DateTimeOffset.UtcNow.AddDays(-1);
            ctx.Add(new Booking(testData.Club.Id, TestDataSeed.Court1Id, testData.Club.PlayModes[0].Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [testData.Member1.Id]));
            await ctx.SaveChangesAsync();
            return (testData.Club.Id, testData.Member1.Id, tennis.Id);
        });

        var result = await SendAsync(new GetMemberBadgeProgress(memberId, clubId));

        result.Tracks.Select(t => t.Sport).Should().Equal(Bookennis.Shared.Controller.Shared.Sport.Tennis, Bookennis.Shared.Controller.Shared.Sport.Padel);
        result.MatchCount.Should().Be(1);
        result.CurrentBadgeTierId.Should().Be(tennisTierId);
        result.Tracks[1].MatchCount.Should().Be(0);
    }

    private async Task<(int ClubId, int MemberId, int BronzeTierId, int SilverTierId, int GoldTierId)> SeedActiveSeasonProgressData(int bookingCount)
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var club = testData.Club;
            var member = testData.Member1;
            var playMode = club.PlayModes[0];

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var bronze = new BadgeTier(club.Id, season.Id, 1, "Bronze", "Bronze badge", 1, 1);
            var silver = new BadgeTier(club.Id, season.Id, 2, "Silver", "Silver badge", 3, 2);
            var gold = new BadgeTier(club.Id, season.Id, 3, "Gold", "Gold badge", 5, 3);
            ctx.AddRange(bronze, silver, gold);
            await ctx.SaveChangesAsync();

            for (var i = 0; i < bookingCount; i++)
            {
                var from = DateTimeOffset.UtcNow.AddDays(-(i + 2));
                ctx.Add(new Booking(
                    club.Id,
                    TestDataSeed.Court1Id,
                    playMode.Id,
                    new DateTimeOffsetInterval(from, from.AddHours(1)),
                    TimeZoneInfo.Utc.Id,
                    [member.Id]
                ));
            }

            await ctx.SaveChangesAsync();

            return (club.Id, member.Id, bronze.Id, silver.Id, gold.Id);
        });
    }
}
