using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
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
