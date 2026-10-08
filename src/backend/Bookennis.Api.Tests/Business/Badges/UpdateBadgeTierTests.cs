using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.BadgeTiers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class UpdateBadgeTierTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateBadgeTier_UpdatesNameDescriptionAndMatchesRequired()
    {
        var (clubId, tierId) = await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            var tier = new BadgeTier(clubId, season.Id, 1, "Before", "Before desc", 2, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();
            return (clubId, tier.Id);
        });

        await SendAsync(new UpdateBadgeTier(clubId, tierId, new UpdateBadgeTierModel("After", "After desc", 7)));

        var updatedTier = await QueryAsync(ctx => ctx.BadgeTiers.SingleAsync(t => t.Id == tierId));

        updatedTier.Name.Should().Be("After");
        updatedTier.Description.Should().Be("After desc");
        updatedTier.MatchesRequired.Should().Be(7);
    }
}
