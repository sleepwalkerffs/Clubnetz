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

public class DeleteBadgeTierTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteBadgeTier_NeverEarned_DeletesSuccessfully()
    {
        var (clubId, tierId) = await SeedTier();

        await SendAsync(new DeleteBadgeTier(clubId, tierId));

        var exists = await QueryAsync(ctx => ctx.BadgeTiers.AnyAsync(t => t.Id == tierId));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBadgeTier_AlreadyEarnedByMember_ThrowsPreconditionException()
    {
        var (clubId, tierId, seasonId) = await SeedTierWithSeason();

        await QueryAsync(async ctx =>
        {
            var memberId = ctx.TestData().Member1.Id;
            ctx.Add(new MemberBadge(memberId, tierId, seasonId));
            await ctx.SaveChangesAsync();
        });

        var act = () => SendAsync(new DeleteBadgeTier(clubId, tierId));
        await act.Should().ThrowAsync<PreconditionException>();

        var exists = await QueryAsync(ctx => ctx.BadgeTiers.AnyAsync(t => t.Id == tierId));
        exists.Should().BeTrue();
    }

    private async Task<(int ClubId, int TierId)> SeedTier()
    {
        var (clubId, tierId, _) = await SeedTierWithSeason();
        return (clubId, tierId);
    }

    private async Task<(int ClubId, int TierId, int SeasonId)> SeedTierWithSeason()
    {
        return await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var tier = new BadgeTier(clubId, season.Id, 1, "Bronze", "Bronze badge", 3, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();

            return (clubId, tier.Id, season.Id);
        });
    }
}
