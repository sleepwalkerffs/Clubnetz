using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class DeleteSeasonTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteSeason_RemovesSeasonFromClub()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var seasonId = await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));

        await SendAsync(new DeleteSeason(seasonId));

        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.Seasons).SingleAsync());
        club.Seasons.Should().NotContain(s => s.Id == seasonId);
    }

    [Fact]
    public async Task DeleteSeason_WithPlansAndUnpaidOrders_RemovesThemWithTheSeason()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());
        var seasonId = await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));
        await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, seasonId);
            await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            var cancelled = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member2Id, TestDataSeed.Member2Id);
            cancelled.CancelByMember();
            await ctx.SaveChangesAsync();
        });

        await SendAsync(new DeleteSeason(seasonId));

        (await QueryAsync(ctx => ctx.Seasons.AnyAsync(s => s.Id == seasonId))).Should().BeFalse();
        (await QueryAsync(ctx => ctx.MembershipPlans.AnyAsync())).Should().BeFalse();
        (await QueryAsync(ctx => ctx.MembershipOrders.AnyAsync())).Should().BeFalse();
    }

    [Theory]
    [InlineData(MembershipOrderStatus.Paid)]
    [InlineData(MembershipOrderStatus.Processing)]
    public async Task DeleteSeason_WithPaidMembershipFees_Throws(MembershipOrderStatus status)
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());
        var seasonId = await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));
        await QueryAsync(async ctx =>
        {
            var order = await MembershipSeed.SeedOrder(ctx, await MembershipSeed.SeedPlan(ctx, seasonId), TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            if (status == MembershipOrderStatus.Paid)
                order.MarkPaid(MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow);
            else
                order.MarkProcessing("pi_1");

            await ctx.SaveChangesAsync();
        });

        var act = () => SendAsync(new DeleteSeason(seasonId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(DeleteSeason.ErrorCode.SeasonHasPayments));
        (await QueryAsync(ctx => ctx.Seasons.AnyAsync(s => s.Id == seasonId))).Should().BeTrue();
        (await QueryAsync(ctx => ctx.MembershipOrders.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task DeleteSeason_NonExistentSeason_ThrowsInvalidOperationException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var act = () => SendAsync(new DeleteSeason(999999));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
