using Bookennis.Api.Business.SubscriptionPlans;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class CreateSubscriptionPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateSubscriptionPlan_ValidInput_CreatesPlanForUser()
    {
        var id = await SendAsync(new CreateSubscriptionPlan(TestDataSeed.ClubId, TestDataSeed.UserId, "Winter", SubscriptionPlanSeed.Start, SubscriptionPlanSeed.End, 4));

        var plan = await QueryAsync(ctx => ctx.SubscriptionPlans.SingleAsync(p => p.Id == id));
        plan.OwnerUserId.Should().Be(TestDataSeed.UserId);
        plan.ClubId.Should().Be(TestDataSeed.ClubId);
        plan.Name.Should().Be("Winter");
        plan.PlayersPerWeek.Should().Be(4);
        plan.ExcludedWeeks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSubscriptionPlan_EndBeforeStart_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new CreateSubscriptionPlan(TestDataSeed.ClubId, TestDataSeed.UserId, "Winter", SubscriptionPlanSeed.End, SubscriptionPlanSeed.Start, 4));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
