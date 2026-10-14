using Bookennis.Api.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Memberships;

public class UpdateMembershipPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateMembershipPlan_ChangesThePlanButNotItsOrders()
    {
        var (planId, orderId) = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, season.Id);
            var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            return (plan.Id, order.Id);
        });

        await SendAsync(new UpdateMembershipPlan(TestDataSeed.ClubId, planId, MembershipSeed.Data("Adult (full member)", 13500) with { RequiresApproval = true }));

        var plan = await QueryAsync(ctx => ctx.MembershipPlans.SingleAsync(p => p.Id == planId));
        plan.Name.Should().Be("Adult (full member)");
        plan.PriceCents.Should().Be(13500);
        plan.RequiresApproval.Should().BeTrue();

        var order = await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync(o => o.Id == orderId));
        order.PlanName.Should().Be("Adult");
        order.AmountCents.Should().Be(12000);
    }

    [Fact]
    public async Task UpdateMembershipPlan_NameOfAnotherPlan_Throws()
    {
        var planId = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Youth"));
            return (await MembershipSeed.SeedPlan(ctx, season.Id)).Id;
        });

        var act = () => SendAsync(new UpdateMembershipPlan(TestDataSeed.ClubId, planId, MembershipSeed.Data("Youth")));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateMembershipPlan.ErrorCode.MembershipPlanNameAlreadyUsed));
    }

    [Fact]
    public async Task UpdateMembershipPlan_KeepingItsOwnName_IsAllowed()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);

        await SendAsync(new UpdateMembershipPlan(TestDataSeed.ClubId, planId, MembershipSeed.Data(priceCents: 9900)));

        (await QueryAsync(ctx => ctx.MembershipPlans.SingleAsync(p => p.Id == planId))).PriceCents.Should().Be(9900);
    }

    [Fact]
    public async Task UpdateMembershipPlan_PlanOfAnotherClub_IsNotFound()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);

        var act = () => SendAsync(new UpdateMembershipPlan(TestDataSeed.ClubId + 1, planId, MembershipSeed.Data()));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
