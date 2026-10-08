using Bookennis.Api.Business.SubscriptionPlans;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.SubscriptionPlans;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class ComputeSubscriptionScheduleTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ComputeSubscriptionSchedule_StoresFairSchedule()
    {
        var planId = await QueryAsync(async ctx =>
            (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, participantCount: 6, excludedWeeks: [SubscriptionPlanSeed.ChristmasWeek])).Id);

        var result = await SendAsync(new ComputeSubscriptionSchedule(TestDataSeed.ClubId, planId, Seed: 7));

        result.HasSchedule.Should().BeTrue();
        result.Weeks.Single(w => w.IsExcluded).ParticipantIds.Should().BeEmpty();
        result.Weeks.Where(w => !w.IsExcluded).Should().AllSatisfy(w => w.ParticipantIds.Should().HaveCount(4));
        (result.Participants.Max(p => p.Assigned) - result.Participants.Min(p => p.Assigned)).Should().BeLessThanOrEqualTo(1);

        var storedCount = await QueryAsync(ctx => ctx.SubscriptionPlanAssignments.CountAsync(a => a.SubscriptionPlanId == planId));
        storedCount.Should().Be(13 * 4);
    }

    [Fact]
    public async Task ComputeSubscriptionSchedule_RespectsUnavailableWeeks()
    {
        var plan = await QueryAsync(ctx => SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, participantCount: 5, playersPerWeek: 2));
        var unavailable = new DateOnly(2026, 10, 5);
        await QueryAsync(async ctx =>
        {
            var tracked = await ctx.SubscriptionPlans.Include(p => p.Participants).Include(p => p.Assignments).SingleAsync(p => p.Id == plan.Id);
            tracked.Update(tracked.Name, tracked.StartDate, tracked.EndDate, tracked.PlayersPerWeek, [],
                tracked.Participants.Select(p => new SubscriptionPlan.ParticipantData(p.Id, p.Name, p.Percentage, p.ColorIndex, [unavailable])).Take(1)
                    .Concat(tracked.Participants.Skip(1).Select(p => new SubscriptionPlan.ParticipantData(p.Id, p.Name, p.Percentage, p.ColorIndex, [])))
                    .ToList());
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new ComputeSubscriptionSchedule(TestDataSeed.ClubId, plan.Id, Seed: null));

        var firstParticipantId = result.Participants[0].Id;
        result.Weeks.Single(w => w.Monday == unavailable).ParticipantIds.Should().NotContain(firstParticipantId);
    }

    [Fact]
    public async Task ComputeSubscriptionSchedule_NotEnoughParticipants_ThrowsPreconditionException()
    {
        var planId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, participantCount: 3, playersPerWeek: 4)).Id);

        var act = () => SendAsync(new ComputeSubscriptionSchedule(TestDataSeed.ClubId, planId, Seed: 1));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanNotEnoughParticipants));
    }
}
