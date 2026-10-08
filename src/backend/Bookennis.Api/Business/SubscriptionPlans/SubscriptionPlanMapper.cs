using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Api.Business.SubscriptionPlans;

public static class SubscriptionPlanMapper
{
    public static SubscriptionPlanDto ToDto(SubscriptionPlan plan)
    {
        var participants = plan.Participants.OrderBy(p => p.SortOrder).ToList();
        var activeWeeks = plan.GetActiveWeeks();
        var activeWeekSet = activeWeeks.ToHashSet();
        var assignmentsByWeek = plan.Assignments
            .GroupBy(a => a.WeekStart)
            .ToDictionary(g => g.Key, g => g.Select(a => a.SubscriptionPlanParticipantId).ToHashSet());

        var expectations = SubscriptionScheduler.CalculateExpectations(activeWeeks, ToSchedulerParticipants(participants), plan.PlayersPerWeek);

        var weeks = plan.GetWeeks()
            .Select(week =>
            {
                var isActive = activeWeekSet.Contains(week.Monday);
                var availableCount = participants.Count(p => !p.UnavailableWeeks.Contains(week.Monday));
                var participantIds = assignmentsByWeek.GetValueOrDefault(week.Monday) ?? [];
                return new SubscriptionWeekDto
                {
                    Monday = week.Monday,
                    WeekNumber = week.WeekNumber,
                    Year = week.Year,
                    IsExcluded = !isActive,
                    IsUnderstaffed = isActive && participants.Count >= plan.PlayersPerWeek && availableCount < plan.PlayersPerWeek,
                    ParticipantIds = participants.Select(p => p.Id).Where(participantIds.Contains).ToList(),
                };
            })
            .ToList();

        var pairs = new List<SubscriptionPairDto>();
        foreach (var first in participants)
        {
            foreach (var second in participants.Where(p => p.Id > first.Id))
            {
                pairs.Add(new SubscriptionPairDto
                {
                    ParticipantId1 = first.Id,
                    ParticipantId2 = second.Id,
                    Count = assignmentsByWeek.Values.Count(ids => ids.Contains(first.Id) && ids.Contains(second.Id)),
                    Expected = Math.Round(expectations.GetExpectedPairCount(first.Id, second.Id), 2),
                });
            }
        }

        return new SubscriptionPlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            PlayersPerWeek = plan.PlayersPerWeek,
            HasSchedule = plan.HasSchedule,
            Weeks = weeks,
            Participants = participants.Select(p => new SubscriptionParticipantDto
            {
                Id = p.Id,
                Name = p.Name,
                Percentage = p.Percentage,
                ColorIndex = p.ColorIndex,
                UnavailableWeeks = p.UnavailableWeeks.ToList(),
                Assigned = plan.Assignments.Count(a => a.SubscriptionPlanParticipantId == p.Id),
                Target = Math.Round(expectations.GetTarget(p.Id), 2),
            }).ToList(),
            Pairs = pairs,
        };
    }

    public static List<SubscriptionScheduler.Participant> ToSchedulerParticipants(IEnumerable<SubscriptionPlanParticipant> participants)
        => participants.Select(p => new SubscriptionScheduler.Participant(p.Id, p.Percentage, p.UnavailableWeeks.ToHashSet())).ToList();
}
