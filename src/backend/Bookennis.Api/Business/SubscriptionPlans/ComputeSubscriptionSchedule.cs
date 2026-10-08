using Bookennis.Api.Data;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Api.Business.SubscriptionPlans;

/// <summary>Computes the schedule. A null <c>Seed</c> computes a new random variant (used by "shuffle").</summary>
public record ComputeSubscriptionSchedule(int ClubId, int SubscriptionPlanId, int? Seed) : ICommand<SubscriptionPlanDto>
{
    public class Handler(AppDbContext context, ISubscriptionScheduler scheduler) : IRequestHandler<ComputeSubscriptionSchedule, SubscriptionPlanDto>
    {
        public async Task<SubscriptionPlanDto> Handle(ComputeSubscriptionSchedule request, CancellationToken cancellationToken)
        {
            var plan = await context.SubscriptionPlans.GetPlanWithDetails(request.ClubId, request.SubscriptionPlanId, cancellationToken);
            plan.EnsureSchedulable();

            var result = scheduler.Schedule(new SubscriptionScheduler.Input(
                plan.GetActiveWeeks(),
                SubscriptionPlanMapper.ToSchedulerParticipants(plan.Participants),
                plan.PlayersPerWeek,
                request.Seed ?? Random.Shared.Next()));

            plan.SetSchedule(result.Assignments.Select(a => new SubscriptionPlan.WeekAssignmentData(a.Key, a.Value.ToList())).ToList());

            await context.SaveChangesAsync(cancellationToken);

            return SubscriptionPlanMapper.ToDto(plan);
        }
    }
}
