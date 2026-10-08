using Bookennis.Api.Data;
using Bookennis.Shared.Controller.SubscriptionPlans;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.SubscriptionPlans;

public record GetSubscriptionPlan(int ClubId, int SubscriptionPlanId) : IQuery<SubscriptionPlanDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetSubscriptionPlan, SubscriptionPlanDto>
    {
        public async Task<SubscriptionPlanDto> Handle(GetSubscriptionPlan request, CancellationToken cancellationToken)
        {
            var plan = await context.SubscriptionPlans.AsNoTracking().GetPlanWithDetails(request.ClubId, request.SubscriptionPlanId, cancellationToken);
            return SubscriptionPlanMapper.ToDto(plan);
        }
    }
}
