using Bookennis.Api.Business.SubscriptionPlans;
using Fusonic.Extensions.Common.Entities;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class GetSubscriptionPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetSubscriptionPlan_ReturnsWeeksParticipantsAndStatistics()
    {
        var planId = await QueryAsync(async ctx =>
            (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, excludedWeeks: [SubscriptionPlanSeed.ChristmasWeek], withSchedule: true)).Id);

        var result = await SendAsync(new GetSubscriptionPlan(TestDataSeed.ClubId, planId));

        result.Weeks.Should().HaveCount(14);
        result.Weeks.Select(w => w.WeekNumber).Should().StartWith([40, 41]).And.EndWith([52, 53]);
        var christmas = result.Weeks.Single(w => w.Monday == SubscriptionPlanSeed.ChristmasWeek);
        christmas.IsExcluded.Should().BeTrue();
        christmas.ParticipantIds.Should().BeEmpty();
        result.Weeks.Where(w => !w.IsExcluded).Should().AllSatisfy(w => w.ParticipantIds.Should().HaveCount(4));

        result.HasSchedule.Should().BeTrue();
        result.Participants.Should().HaveCount(5);
        result.Participants.Sum(p => p.Assigned).Should().Be(13 * 4);
        result.Participants.Should().AllSatisfy(p => p.Target.Should().BeApproximately(13 * 4 / 5.0, 0.01));
        result.Pairs.Should().HaveCount(10);
        result.Pairs.Sum(p => p.Count).Should().Be(13 * 6);
    }

    [Fact]
    public async Task GetSubscriptionPlan_OtherClub_ThrowsNotFound()
    {
        var planId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId)).Id);

        var act = () => SendAsync(new GetSubscriptionPlan(TestDataSeed.ClubId + 1, planId));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
