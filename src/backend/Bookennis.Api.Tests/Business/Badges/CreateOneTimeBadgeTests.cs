using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class CreateOneTimeBadgeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateOneTimeBadge_CreatesBadge()
    {
        var (clubId, seasonId) = await SeedSeason();

        var badgeId = await SendAsync(new CreateOneTimeBadge(clubId, seasonId, "Singles Champion Men", "Winner of the men's singles tournament"));

        var badge = await QueryAsync(ctx => ctx.OneTimeBadges.SingleAsync(b => b.Id == badgeId));
        badge.Name.Should().Be("Singles Champion Men");
        badge.Description.Should().Be("Winner of the men's singles tournament");
        badge.SeasonId.Should().Be(seasonId);
    }

    [Fact]
    public async Task CreateOneTimeBadge_DuplicateNameInSeason_ThrowsPreconditionException()
    {
        var (clubId, seasonId) = await SeedSeason();
        await SendAsync(new CreateOneTimeBadge(clubId, seasonId, "Singles Champion Men", "Description"));

        var act = () => SendAsync(new CreateOneTimeBadge(clubId, seasonId, "Singles Champion Men", "Other description"));
        await act.Should().ThrowAsync<PreconditionException>();
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
