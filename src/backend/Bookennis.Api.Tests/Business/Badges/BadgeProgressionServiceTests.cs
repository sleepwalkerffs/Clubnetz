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

    [Fact]
    public async Task CheckAndAwardBadges_TracksPerSport_CountOnlyTheMatchesOfTheirSport()
    {
        // 2 matches on the tennis court, 1 on the padel court
        var (clubId, memberId, seasonId) = await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var club = testData.Club;
            testData.Court2.SetSport(Sport.Padel);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var playMode = club.PlayModes[0];
            AddBooking(ctx, club.Id, TestDataSeed.Court1Id, playMode.Id, -2, testData.Member1.Id);
            AddBooking(ctx, club.Id, TestDataSeed.Court1Id, playMode.Id, -3, testData.Member1.Id);
            AddBooking(ctx, club.Id, TestDataSeed.Court2Id, playMode.Id, -4, testData.Member1.Id);
            await ctx.SaveChangesAsync();
            return (club.Id, testData.Member1.Id, season.Id);
        });

        var tierIds = await QueryAsync(async ctx =>
        {
            var tiers = new[]
            {
                new BadgeTier(clubId, seasonId, 1, "All 3", "", 3, 1),
                new BadgeTier(clubId, seasonId, 1, "Tennis 2", "", 2, 1, Sport.Tennis),
                new BadgeTier(clubId, seasonId, 2, "Tennis 3", "", 3, 2, Sport.Tennis),
                new BadgeTier(clubId, seasonId, 1, "Padel 1", "", 1, 1, Sport.Padel),
                new BadgeTier(clubId, seasonId, 2, "Padel 2", "", 2, 2, Sport.Padel),
            };
            ctx.AddRange(tiers);
            await ctx.SaveChangesAsync();
            return tiers.ToDictionary(t => t.Name, t => t.Id);
        });

        await ScopedAsync(() => GetInstance<IBadgeProgressionService>().CheckAndAwardBadges(memberId, clubId, seasonId, CancellationToken.None));

        var earned = await QueryAsync(ctx => ctx.MemberBadges.Where(b => b.MemberId == memberId).Select(b => b.BadgeTierId).ToListAsync());
        earned.Should().BeEquivalentTo([tierIds["All 3"], tierIds["Tennis 2"], tierIds["Padel 1"]]);
    }

    private static void AddBooking(Data.AppDbContext ctx, int clubId, int courtId, int playModeId, int daysAgo, int memberId)
    {
        var from = DateTimeOffset.UtcNow.AddDays(daysAgo);
        ctx.Add(new Booking(clubId, courtId, playModeId, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [memberId]));
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
