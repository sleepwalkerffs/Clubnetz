using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class GetBadgeTiersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetBadgeTiers_NoConfiguredTiers_ReturnsEmpty()
    {
        var (clubId, seasonId) = await SeedSeason();

        var result = await SendAsync(new GetBadgeTiers(clubId, seasonId));

        result.Tiers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBadgeTiers_WithConfiguredTiers_ReturnsOrderedByLevel()
    {
        var (clubId, seasonId) = await SeedSeason();

        await QueryAsync(async ctx =>
        {
            ctx.AddRange(
                new BadgeTier(clubId, seasonId, 3, "Gold", "Gold badge", 10, 3),
                new BadgeTier(clubId, seasonId, 1, "Bronze", "Bronze badge", 3, 1),
                new BadgeTier(clubId, seasonId, 2, "Silver", "Silver badge", 6, 2)
            );
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetBadgeTiers(clubId, seasonId));

        result.Tiers.Select(t => t.Level).Should().Equal(1, 2, 3);
        result.Tiers.Select(t => t.Name).Should().Equal("Bronze", "Silver", "Gold");
    }

    [Fact]
    public async Task GetBadgeTiers_ReturnsNumberOfMembersThatEarnedTheTier()
    {
        var (clubId, seasonId) = await SeedSeason();

        await QueryAsync(async ctx =>
        {
            var bronze = new BadgeTier(clubId, seasonId, 1, "Bronze", "Bronze badge", 3, 1);
            var silver = new BadgeTier(clubId, seasonId, 2, "Silver", "Silver badge", 6, 2);
            ctx.AddRange(bronze, silver);
            await ctx.SaveChangesAsync();

            ctx.AddRange(
                new MemberBadge(ctx.TestData().Member1.Id, bronze.Id, seasonId),
                new MemberBadge(ctx.TestData().Member2.Id, bronze.Id, seasonId));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetBadgeTiers(clubId, seasonId));

        result.Tiers.Single(t => t.Name == "Bronze").EarnedCount.Should().Be(2);
        result.Tiers.Single(t => t.Name == "Silver").EarnedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBadgeTiers_TiersInOtherSeason_AreExcluded()
    {
        var (clubId, seasonId) = await SeedSeason();

        await QueryAsync(async ctx =>
        {
            var otherSeason = new Season(clubId, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            ctx.Add(otherSeason);
            ctx.Add(new BadgeTier(clubId, seasonId, 1, "Bronze", "Bronze badge", 3, 1));
            await ctx.SaveChangesAsync();
            ctx.Add(new BadgeTier(clubId, otherSeason.Id, 1, "Old Bronze", "Old bronze badge", 3, 1));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetBadgeTiers(clubId, seasonId));

        result.Tiers.Should().ContainSingle(t => t.Name == "Bronze");
    }

    private async Task<(int ClubId, int SeasonId)> SeedSeason()
    {
        return await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            return (clubId, season.Id);
        });
    }
}
