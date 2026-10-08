using Bookennis.Api.Data;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Api.Business.SubscriptionPlans;

/// <summary>Replaces all planning inputs. The schedule is cleared if an input relevant for scheduling changed.</summary>
public record UpdateSubscriptionPlan(
    int ClubId,
    int SubscriptionPlanId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int PlayersPerWeek,
    IReadOnlyCollection<DateOnly> ExcludedWeeks,
    IReadOnlyCollection<SubscriptionPlan.ParticipantData> Participants) : ICommand<SubscriptionPlanDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateSubscriptionPlan, SubscriptionPlanDto>
    {
        public async Task<SubscriptionPlanDto> Handle(UpdateSubscriptionPlan request, CancellationToken cancellationToken)
        {
            var plan = await context.SubscriptionPlans.GetPlanWithDetails(request.ClubId, request.SubscriptionPlanId, cancellationToken);

            plan.Update(request.Name, request.StartDate, request.EndDate, request.PlayersPerWeek, request.ExcludedWeeks, request.Participants);

            await context.SaveChangesAsync(cancellationToken);

            return SubscriptionPlanMapper.ToDto(plan);
        }
    }
}
