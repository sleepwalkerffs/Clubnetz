using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class CopyOneTimeBadgesTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CopyOneTimeBadges_EmptyTargetSeason_CopiesBadgesAndImagesButNotAwardees()
    {
        var (clubId, sourceSeasonId, targetSeasonId, sourceBadgeId) = await SeedSourceSeasonWithBadgeImageAndAwardee();

        await SendAsync(new CopyOneTimeBadges(clubId, sourceSeasonId, targetSeasonId));

        var copiedBadge = await QueryAsync(ctx => ctx.OneTimeBadges.SingleAsync(b => b.ClubId == clubId && b.SeasonId == targetSeasonId));
        copiedBadge.Name.Should().Be("Singles Champion Men");
        copiedBadge.Description.Should().Be("Winner of the men's singles tournament");

        var copiedImage = await QueryAsync(ctx => ctx.OneTimeBadgeImages.SingleAsync(i => i.OneTimeBadgeId == copiedBadge.Id));
        copiedImage.ContentType.Should().Be("image/jpeg");
        copiedImage.Data.Should().Equal([1, 2, 3]);

        var copiedBadgeAwardeeCount = await QueryAsync(ctx => ctx.MemberOneTimeBadges.CountAsync(motb => motb.OneTimeBadgeId == copiedBadge.Id));
        copiedBadgeAwardeeCount.Should().Be(0, "awardees must never carry over when copying badge categories to a new season");

        var sourceBadgeAwardeeCount = await QueryAsync(ctx => ctx.MemberOneTimeBadges.CountAsync(motb => motb.OneTimeBadgeId == sourceBadgeId));
        sourceBadgeAwardeeCount.Should().Be(1, "the source season's award history must be untouched");
    }

    [Fact]
    public async Task CopyOneTimeBadges_SourceSeasonHasNoBadges_ThrowsPreconditionException()
    {
        var (clubId, _, targetSeasonId, _) = await SeedSourceSeasonWithBadgeImageAndAwardee();
        var emptySeasonId = await QueryAsync(async ctx =>
        {
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        var act = () => SendAsync(new CopyOneTimeBadges(clubId, emptySeasonId, targetSeasonId));
        await act.Should().ThrowAsync<PreconditionException>();
    }

    [Fact]
    public async Task CopyOneTimeBadges_TargetSeasonAlreadyHasBadges_ThrowsPreconditionException()
    {
        var (clubId, sourceSeasonId, targetSeasonId, _) = await SeedSourceSeasonWithBadgeImageAndAwardee();
        await QueryAsync(async ctx =>
        {
            ctx.Add(new OneTimeBadge(clubId, targetSeasonId, "Existing", "Existing badge"));
            await ctx.SaveChangesAsync();
        });

        var act = () => SendAsync(new CopyOneTimeBadges(clubId, sourceSeasonId, targetSeasonId));
        await act.Should().ThrowAsync<PreconditionException>();

        var targetBadgeCount = await QueryAsync(ctx => ctx.OneTimeBadges.CountAsync(b => b.SeasonId == targetSeasonId));
        targetBadgeCount.Should().Be(1);
    }

    private async Task<(int ClubId, int SourceSeasonId, int TargetSeasonId, int SourceBadgeId)> SeedSourceSeasonWithBadgeImageAndAwardee()
    {
        return await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var sourceSeason = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            var targetSeason = new Season(clubId, new DateOnlyInterval(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
            ctx.AddRange(sourceSeason, targetSeason);
            await ctx.SaveChangesAsync();

            var badge = new OneTimeBadge(clubId, sourceSeason.Id, "Singles Champion Men", "Winner of the men's singles tournament");
            ctx.Add(badge);
            await ctx.SaveChangesAsync();

            ctx.Add(new OneTimeBadgeImage(badge.Id, [1, 2, 3], "image/jpeg"));
            ctx.Add(new MemberOneTimeBadge(ctx.TestData().Member1.Id, badge.Id));
            await ctx.SaveChangesAsync();

            return (clubId, sourceSeason.Id, targetSeason.Id, badge.Id);
        });
    }
}
