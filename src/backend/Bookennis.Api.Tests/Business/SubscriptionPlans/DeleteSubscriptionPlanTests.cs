using Bookennis.Api.Business.SubscriptionPlans;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class DeleteSubscriptionPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteSubscriptionPlan_RemovesPlanWithParticipantsAndSchedule()
    {
        var planId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, withSchedule: true)).Id);

        await SendAsync(new DeleteSubscriptionPlan(TestDataSeed.ClubId, planId));

        (await QueryAsync(ctx => ctx.SubscriptionPlans.AnyAsync(p => p.Id == planId))).Should().BeFalse();
        (await QueryAsync(ctx => ctx.SubscriptionPlanParticipants.AnyAsync(p => p.SubscriptionPlanId == planId))).Should().BeFalse();
        (await QueryAsync(ctx => ctx.SubscriptionPlanAssignments.AnyAsync(a => a.SubscriptionPlanId == planId))).Should().BeFalse();
    }
}
