using Bookennis.Api.Business.SubscriptionPlans;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class GetSubscriptionPlansTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetSubscriptionPlans_ReturnsOnlyPlansOfUser()
    {
        var ownPlanId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, withSchedule: true)).Id);
        await QueryAsync(ctx => SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.AdminId));

        var result = await SendAsync(new GetSubscriptionPlans(TestDataSeed.ClubId, TestDataSeed.UserId));

        var plan = result.Plans.Should().ContainSingle().Subject;
        plan.Id.Should().Be(ownPlanId);
        plan.ParticipantCount.Should().Be(5);
        plan.HasSchedule.Should().BeTrue();
        plan.PlayersPerWeek.Should().Be(4);
    }

    [Fact]
    public async Task GetSubscriptionPlans_NoPlans_ReturnsEmpty()
    {
        var result = await SendAsync(new GetSubscriptionPlans(TestDataSeed.ClubId, TestDataSeed.UserId));

        result.Plans.Should().BeEmpty();
    }
}
