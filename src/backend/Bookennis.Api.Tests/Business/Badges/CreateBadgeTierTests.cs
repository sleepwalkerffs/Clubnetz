using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class CreateBadgeTierTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateBadgeTier_FirstTierInSeason_AssignsLevelOne()
    {
        var (clubId, seasonId) = await SeedSeason();

        var tierId = await SendAsync(new CreateBadgeTier(clubId, seasonId, "Bronze", "Bronze badge", 3));

        var tier = await QueryAsync(ctx => ctx.BadgeTiers.SingleAsync(t => t.Id == tierId));
        tier.Level.Should().Be(1);
        tier.SortOrder.Should().Be(1);
        tier.Name.Should().Be("Bronze");
        tier.Description.Should().Be("Bronze badge");
        tier.MatchesRequired.Should().Be(3);
    }

    [Fact]
    public async Task CreateBadgeTier_SubsequentTier_IncrementsLevel()
    {
        var (clubId, seasonId) = await SeedSeason();
        await SendAsync(new CreateBadgeTier(clubId, seasonId, "Bronze", "Bronze badge", 3));

        var secondTierId = await SendAsync(new CreateBadgeTier(clubId, seasonId, "Silver", "Silver badge", 6));

        var tier = await QueryAsync(ctx => ctx.BadgeTiers.SingleAsync(t => t.Id == secondTierId));
        tier.Level.Should().Be(2);
        tier.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task CreateBadgeTier_ScopedPerSeason_DoesNotCollideWithOtherSeason()
    {
        var (clubId, seasonId) = await SeedSeason();
        var otherSeasonId = await QueryAsync(async ctx =>
        {
            var otherSeason = new Season(clubId, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            ctx.Add(otherSeason);
            await ctx.SaveChangesAsync();
            return otherSeason.Id;
        });

        await SendAsync(new CreateBadgeTier(clubId, otherSeasonId, "Old Bronze", "Old bronze badge", 3));
        var tierId = await SendAsync(new CreateBadgeTier(clubId, seasonId, "Bronze", "Bronze badge", 3));

        var tier = await QueryAsync(ctx => ctx.BadgeTiers.SingleAsync(t => t.Id == tierId));
        tier.Level.Should().Be(1);
        tier.SeasonId.Should().Be(seasonId);
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
