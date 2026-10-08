using Bookennis.Api.Business.SubscriptionPlans;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.SubscriptionPlans;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class UpdateSubscriptionScheduleTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateSubscriptionSchedule_SwappedPlayer_IsStored()
    {
        var planId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, withSchedule: true)).Id);
        var plan = await SendAsync(new GetSubscriptionPlan(TestDataSeed.ClubId, planId));
        var week = plan.Weeks[0];
        var benched = plan.Participants.Select(p => p.Id).Except(week.ParticipantIds).First();
        var swapped = week.ParticipantIds.Skip(1).Append(benched).ToList();

        var weeks = plan.Weeks
            .Select(w => new SubscriptionPlan.WeekAssignmentData(w.Monday, w.Monday == week.Monday ? swapped : w.ParticipantIds))
            .ToList();

        var result = await SendAsync(new UpdateSubscriptionSchedule(TestDataSeed.ClubId, planId, weeks));

        result.Weeks[0].ParticipantIds.Should().BeEquivalentTo(swapped);
        var reloaded = await SendAsync(new GetSubscriptionPlan(TestDataSeed.ClubId, planId));
        reloaded.Weeks[0].ParticipantIds.Should().BeEquivalentTo(swapped);
        reloaded.Weeks.Skip(1).Select(w => w.ParticipantIds).Should().BeEquivalentTo(plan.Weeks.Skip(1).Select(w => w.ParticipantIds));
    }

    [Fact]
    public async Task UpdateSubscriptionSchedule_UnknownParticipant_ThrowsPreconditionException()
    {
        var planId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId)).Id);

        var act = () => SendAsync(new UpdateSubscriptionSchedule(TestDataSeed.ClubId, planId, [new SubscriptionPlan.WeekAssignmentData(new DateOnly(2026, 10, 5), [-1])]));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanInvalidAssignment));
    }
}
