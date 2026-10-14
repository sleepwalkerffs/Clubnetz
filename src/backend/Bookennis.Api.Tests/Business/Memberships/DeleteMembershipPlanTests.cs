using Bookennis.Api.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Memberships;

public class DeleteMembershipPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteMembershipPlan_WithoutOrders_RemovesThePlan()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);

        await SendAsync(new DeleteMembershipPlan(TestDataSeed.ClubId, planId));

        (await QueryAsync(ctx => ctx.MembershipPlans.AnyAsync(p => p.Id == planId))).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteMembershipPlan_WithAnOrderEvenACancelledOne_Throws()
    {
        var planId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            order.CancelByMember();
            await ctx.SaveChangesAsync();
            return plan.Id;
        });

        var act = () => SendAsync(new DeleteMembershipPlan(TestDataSeed.ClubId, planId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(DeleteMembershipPlan.ErrorCode.MembershipPlanHasOrders));
        (await QueryAsync(ctx => ctx.MembershipPlans.AnyAsync(p => p.Id == planId))).Should().BeTrue();
    }
}
