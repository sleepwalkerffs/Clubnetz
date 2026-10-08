using Bookennis.Api.Data;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Api.Business.SubscriptionPlans;

/// <summary>Stores a manually adjusted schedule (e.g. after swapping players).</summary>
public record UpdateSubscriptionSchedule(int ClubId, int SubscriptionPlanId, IReadOnlyCollection<SubscriptionPlan.WeekAssignmentData> Weeks) : ICommand<SubscriptionPlanDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateSubscriptionSchedule, SubscriptionPlanDto>
    {
        public async Task<SubscriptionPlanDto> Handle(UpdateSubscriptionSchedule request, CancellationToken cancellationToken)
        {
            var plan = await context.SubscriptionPlans.GetPlanWithDetails(request.ClubId, request.SubscriptionPlanId, cancellationToken);

            plan.SetSchedule(request.Weeks);

            await context.SaveChangesAsync(cancellationToken);

            return SubscriptionPlanMapper.ToDto(plan);
        }
    }
}
