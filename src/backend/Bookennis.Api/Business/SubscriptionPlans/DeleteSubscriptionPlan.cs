using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.SubscriptionPlans;

public record DeleteSubscriptionPlan(int ClubId, int SubscriptionPlanId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteSubscriptionPlan>
    {
        public async Task<Unit> Handle(DeleteSubscriptionPlan request, CancellationToken cancellationToken)
        {
            var plan = await context.SubscriptionPlans
                .SingleRequiredAsync(p => p.Id == request.SubscriptionPlanId && p.ClubId == request.ClubId, cancellationToken);

            context.SubscriptionPlans.Remove(plan);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
