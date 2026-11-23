using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
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

    [Fact]
    public async Task CreateBadgeTier_WithSport_NumbersLevelsPerTrack()
    {
        var (clubId, seasonId) = await SeedSeason();
        await SendAsync(new CreateBadgeTier(clubId, seasonId, "Bronze", "Every sport", 3));
        await SendAsync(new CreateBadgeTier(clubId, seasonId, "Silver", "Every sport", 6));
        await SendAsync(new CreateBadgeTier(clubId, seasonId, "Tennis Bronze", "Tennis", 3, Sport.Tennis));

        var padelBronzeId = await SendAsync(new CreateBadgeTier(clubId, seasonId, "Padel Bronze", "Padel", 2, Sport.Padel));
        var padelSilverId = await SendAsync(new CreateBadgeTier(clubId, seasonId, "Padel Silver", "Padel", 4, Sport.Padel));

        var tiers = await QueryAsync(ctx => ctx.BadgeTiers.Where(t => t.SeasonId == seasonId).ToListAsync());
        tiers.Where(t => t.Sport == null).Select(t => t.Level).Should().BeEquivalentTo([1, 2]);
        tiers.Single(t => t.Sport == Sport.Tennis).Level.Should().Be(1);
        tiers.Single(t => t.Id == padelBronzeId).Level.Should().Be(1);
        tiers.Single(t => t.Id == padelSilverId).Should().Match<BadgeTier>(t => t.Level == 2 && t.Sport == Sport.Padel);
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
