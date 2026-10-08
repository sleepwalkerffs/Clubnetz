using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class CopyBadgeTiersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CopyBadgeTiers_EmptyTargetSeason_CopiesTiersAndImages()
    {
        var (clubId, sourceSeasonId, targetSeasonId, sourceTierId) = await SeedSourceSeasonWithTierAndImage();

        await SendAsync(new CopyBadgeTiers(clubId, sourceSeasonId, targetSeasonId));

        var copiedTier = await QueryAsync(ctx => ctx.BadgeTiers.SingleAsync(t => t.ClubId == clubId && t.SeasonId == targetSeasonId));
        copiedTier.Name.Should().Be("Bronze");
        copiedTier.Description.Should().Be("Bronze badge");
        copiedTier.MatchesRequired.Should().Be(3);
        copiedTier.Level.Should().Be(1);
        copiedTier.SortOrder.Should().Be(1);

        var copiedImage = await QueryAsync(ctx => ctx.BadgeTierImages.SingleAsync(i => i.BadgeTierId == copiedTier.Id));
        copiedImage.ContentType.Should().Be("image/jpeg");
        copiedImage.Data.Should().Equal([1, 2, 3]);

        var sourceTierStillExists = await QueryAsync(ctx => ctx.BadgeTiers.AnyAsync(t => t.Id == sourceTierId));
        sourceTierStillExists.Should().BeTrue();
    }

    [Fact]
    public async Task CopyBadgeTiers_SourceSeasonHasNoTiers_ThrowsPreconditionException()
    {
        var (clubId, _, targetSeasonId, _) = await SeedSourceSeasonWithTierAndImage();
        var emptySeasonId = await QueryAsync(async ctx =>
        {
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        var act = () => SendAsync(new CopyBadgeTiers(clubId, emptySeasonId, targetSeasonId));
        await act.Should().ThrowAsync<PreconditionException>();
    }

    [Fact]
    public async Task CopyBadgeTiers_TargetSeasonAlreadyHasTiers_ThrowsPreconditionException()
    {
        var (clubId, sourceSeasonId, targetSeasonId, _) = await SeedSourceSeasonWithTierAndImage();
        await QueryAsync(async ctx =>
        {
            ctx.Add(new BadgeTier(clubId, targetSeasonId, 1, "Existing", "Existing badge", 1, 1));
            await ctx.SaveChangesAsync();
        });

        var act = () => SendAsync(new CopyBadgeTiers(clubId, sourceSeasonId, targetSeasonId));
        await act.Should().ThrowAsync<PreconditionException>();

        var targetTierCount = await QueryAsync(ctx => ctx.BadgeTiers.CountAsync(t => t.SeasonId == targetSeasonId));
        targetTierCount.Should().Be(1);
    }

    private async Task<(int ClubId, int SourceSeasonId, int TargetSeasonId, int SourceTierId)> SeedSourceSeasonWithTierAndImage()
    {
        return await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var sourceSeason = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            var targetSeason = new Season(clubId, new DateOnlyInterval(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
            ctx.AddRange(sourceSeason, targetSeason);
            await ctx.SaveChangesAsync();

            var tier = new BadgeTier(clubId, sourceSeason.Id, 1, "Bronze", "Bronze badge", 3, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();

            ctx.Add(new BadgeTierImage(tier.Id, [1, 2, 3], "image/jpeg"));
            await ctx.SaveChangesAsync();

            return (clubId, sourceSeason.Id, targetSeason.Id, tier.Id);
        });
    }
}
