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

public class DeleteOneTimeBadgeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteOneTimeBadge_NeverAwarded_DeletesSuccessfully()
    {
        var (clubId, badgeId) = await SeedBadge();

        await SendAsync(new DeleteOneTimeBadge(clubId, badgeId));

        var exists = await QueryAsync(ctx => ctx.OneTimeBadges.AnyAsync(b => b.Id == badgeId));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteOneTimeBadge_AlreadyAwarded_ThrowsPreconditionException()
    {
        var (clubId, badgeId) = await SeedBadge();

        await QueryAsync(async ctx =>
        {
            var memberId = ctx.TestData().Member1.Id;
            ctx.Add(new MemberOneTimeBadge(memberId, badgeId));
            await ctx.SaveChangesAsync();
        });

        var act = () => SendAsync(new DeleteOneTimeBadge(clubId, badgeId));
        await act.Should().ThrowAsync<PreconditionException>();

        var exists = await QueryAsync(ctx => ctx.OneTimeBadges.AnyAsync(b => b.Id == badgeId));
        exists.Should().BeTrue();
    }

    private async Task<(int ClubId, int BadgeId)> SeedBadge()
    {
        return await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var badge = new OneTimeBadge(clubId, season.Id, "Singles Champion Men", "Description");
            ctx.Add(badge);
            await ctx.SaveChangesAsync();

            return (clubId, badge.Id);
        });
    }
}
