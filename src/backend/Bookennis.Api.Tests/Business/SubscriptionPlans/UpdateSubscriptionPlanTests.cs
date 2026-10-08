using Bookennis.Api.Business.SubscriptionPlans;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.SubscriptionPlans;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class UpdateSubscriptionPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateSubscriptionPlan_AddsUpdatesAndRemovesParticipants()
    {
        var plan = await QueryAsync(ctx => SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, participantCount: 3));
        var participants = plan.Participants.ToList();
        var unavailable = new DateOnly(2026, 10, 12);

        var result = await SendAsync(new UpdateSubscriptionPlan(
            TestDataSeed.ClubId,
            plan.Id,
            "Renamed",
            SubscriptionPlanSeed.Start,
            SubscriptionPlanSeed.End,
            2,
            [SubscriptionPlanSeed.ChristmasWeek],
            [
                new SubscriptionPlan.ParticipantData(participants[0].Id, "Anna", 50, 3, [unavailable]),
                new SubscriptionPlan.ParticipantData(participants[1].Id, "Ben", 100, 1, []),
                new SubscriptionPlan.ParticipantData(null, "Carla", 100, 2, []),
            ]));

        result.Name.Should().Be("Renamed");
        result.PlayersPerWeek.Should().Be(2);
        result.Participants.Select(p => p.Name).Should().Equal("Anna", "Ben", "Carla");
        result.Participants[0].Id.Should().Be(participants[0].Id);
        result.Participants[0].Percentage.Should().Be(50);
        result.Participants[0].UnavailableWeeks.Should().Equal(unavailable);
        result.Participants[2].Id.Should().BePositive();
        result.Weeks.Single(w => w.Monday == SubscriptionPlanSeed.ChristmasWeek).IsExcluded.Should().BeTrue();

        var stored = await QueryAsync(ctx => ctx.SubscriptionPlanParticipants.Where(p => p.SubscriptionPlanId == plan.Id).Select(p => p.Name).ToListAsync());
        stored.Should().BeEquivalentTo(["Anna", "Ben", "Carla"]);
    }

    [Fact]
    public async Task UpdateSubscriptionPlan_ChangedUnavailableWeeks_ClearsSchedule()
    {
        var plan = await QueryAsync(ctx => SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, withSchedule: true));

        var result = await SendAsync(new UpdateSubscriptionPlan(
            TestDataSeed.ClubId,
            plan.Id,
            plan.Name,
            plan.StartDate,
            plan.EndDate,
            plan.PlayersPerWeek,
            [],
            plan.Participants.Select((p, i) => new SubscriptionPlan.ParticipantData(p.Id, p.Name, p.Percentage, p.ColorIndex, i == 0 ? [SubscriptionPlanSeed.Start] : [])).ToList()));

        result.HasSchedule.Should().BeFalse();
        (await QueryAsync(ctx => ctx.SubscriptionPlanAssignments.AnyAsync(a => a.SubscriptionPlanId == plan.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSubscriptionPlan_OnlyRenamedParticipant_KeepsSchedule()
    {
        var plan = await QueryAsync(ctx => SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, withSchedule: true));

        var result = await SendAsync(new UpdateSubscriptionPlan(
            TestDataSeed.ClubId,
            plan.Id,
            plan.Name,
            plan.StartDate,
            plan.EndDate,
            plan.PlayersPerWeek,
            [],
            plan.Participants.Select(p => new SubscriptionPlan.ParticipantData(p.Id, p.Name + " X", p.Percentage, p.ColorIndex + 1, [])).ToList()));

        result.HasSchedule.Should().BeTrue();
        result.Participants.Should().AllSatisfy(p => p.Name.Should().EndWith(" X"));
    }

    [Fact]
    public async Task UpdateSubscriptionPlan_DuplicateNames_ThrowsPreconditionException()
    {
        var plan = await QueryAsync(ctx => SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, participantCount: 2));

        var act = () => SendAsync(new UpdateSubscriptionPlan(
            TestDataSeed.ClubId,
            plan.Id,
            plan.Name,
            plan.StartDate,
            plan.EndDate,
            plan.PlayersPerWeek,
            [],
            plan.Participants.Select(p => new SubscriptionPlan.ParticipantData(p.Id, "Same", p.Percentage, p.ColorIndex, [])).ToList()));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
