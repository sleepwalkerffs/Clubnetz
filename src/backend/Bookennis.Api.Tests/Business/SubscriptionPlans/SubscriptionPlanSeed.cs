using Bookennis.Api.Data;
using Bookennis.Domain.SubscriptionPlans;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

internal static class SubscriptionPlanSeed
{
    /// <summary>Thursday of week 40/2026.</summary>
    public static readonly DateOnly Start = new(2026, 10, 1);

    /// <summary>Thursday of week 53/2026, so the plan spans 14 weeks.</summary>
    public static readonly DateOnly End = new(2026, 12, 31);

    public static readonly DateOnly ChristmasWeek = new(2026, 12, 21);

    public static async Task<SubscriptionPlan> SeedPlan(
        AppDbContext context,
        int userId,
        int participantCount = 5,
        int playersPerWeek = 4,
        IReadOnlyCollection<DateOnly>? excludedWeeks = null,
        bool withSchedule = false)
    {
        var clubId = TestDataSeed.ClubId;
        var plan = new SubscriptionPlan(clubId, userId, "Winter 26/27", Start, End, playersPerWeek);
        plan.Update(
            plan.Name,
            Start,
            End,
            playersPerWeek,
            excludedWeeks ?? [],
            Enumerable.Range(1, participantCount)
                .Select(i => new SubscriptionPlan.ParticipantData(null, $"Player {i}", 100, i - 1, []))
                .ToList());

        context.SubscriptionPlans.Add(plan);
        await context.SaveChangesAsync();

        if (withSchedule)
        {
            var result = new SubscriptionScheduler().Schedule(new SubscriptionScheduler.Input(
                plan.GetActiveWeeks(),
                plan.Participants.Select(p => new SubscriptionScheduler.Participant(p.Id, p.Percentage, p.UnavailableWeeks.ToHashSet())).ToList(),
                plan.PlayersPerWeek,
                Seed: 1));
            plan.SetSchedule(result.Assignments.Select(a => new SubscriptionPlan.WeekAssignmentData(a.Key, a.Value.ToList())).ToList());
            await context.SaveChangesAsync();
        }

        return plan;
    }
}
