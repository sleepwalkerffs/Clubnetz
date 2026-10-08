using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class BadgeProgressionServiceTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CheckAndAwardBadges_DifferentSeasonsWithDifferentTierSets_AwardIndependently()
    {
        var seeded = await SeedTwoSeasonsWithDistinctTierSets();

        await ScopedAsync(() => GetInstance<IBadgeProgressionService>()
            .CheckAndAwardBadges(seeded.MemberId, seeded.ClubId, seeded.SeasonAId, CancellationToken.None));
        await ScopedAsync(() => GetInstance<IBadgeProgressionService>()
            .CheckAndAwardBadges(seeded.MemberId, seeded.ClubId, seeded.SeasonBId, CancellationToken.None));

        var seasonABadges = await QueryAsync(ctx => ctx.MemberBadges
            .Where(b => b.MemberId == seeded.MemberId && b.SeasonId == seeded.SeasonAId)
            .Select(b => b.BadgeTierId)
            .ToListAsync());
        var seasonBBadges = await QueryAsync(ctx => ctx.MemberBadges
            .Where(b => b.MemberId == seeded.MemberId && b.SeasonId == seeded.SeasonBId)
            .Select(b => b.BadgeTierId)
            .ToListAsync());

        // Season A: 3 matches, tier requires 2 -> earned. Season B: 1 match, tier requires 2 -> not earned.
        seasonABadges.Should().Equal(seeded.SeasonATierId);
        seasonBBadges.Should().BeEmpty();
    }

    private async Task<(int ClubId, int MemberId, int SeasonAId, int SeasonBId, int SeasonATierId)> SeedTwoSeasonsWithDistinctTierSets()
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var club = testData.Club;
            var member = testData.Member1;
            var playMode = club.PlayModes[0];

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var seasonA = new Season(club.Id, new DateOnlyInterval(today.AddDays(-100), today.AddDays(-50)));
            var seasonB = new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.AddRange(seasonA, seasonB);
            await ctx.SaveChangesAsync();

            var tierA = new BadgeTier(club.Id, seasonA.Id, 1, "Season A Tier", "Requires 2", 2, 1);
            var tierB = new BadgeTier(club.Id, seasonB.Id, 1, "Season B Tier", "Requires 2", 2, 1);
            ctx.AddRange(tierA, tierB);
            await ctx.SaveChangesAsync();

            // 3 bookings within season A's period
            for (var i = 0; i < 3; i++)
            {
                var from = DateTimeOffset.UtcNow.AddDays(-60 - i);
                ctx.Add(new Booking(club.Id, TestDataSeed.Court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [member.Id]));
            }

            // 1 booking within season B's period
            var seasonBFrom = DateTimeOffset.UtcNow.AddDays(-2);
            ctx.Add(new Booking(club.Id, TestDataSeed.Court1Id, playMode.Id, new DateTimeOffsetInterval(seasonBFrom, seasonBFrom.AddHours(1)), TimeZoneInfo.Utc.Id, [member.Id]));

            await ctx.SaveChangesAsync();

            return (club.Id, member.Id, seasonA.Id, seasonB.Id, tierA.Id);
        });
    }
}
