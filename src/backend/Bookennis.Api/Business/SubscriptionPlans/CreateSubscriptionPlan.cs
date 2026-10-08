using Bookennis.Api.Data;
using Bookennis.Domain.SubscriptionPlans;

namespace Bookennis.Api.Business.SubscriptionPlans;

public record CreateSubscriptionPlan(int ClubId, int UserId, string Name, DateOnly StartDate, DateOnly EndDate, int PlayersPerWeek) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateSubscriptionPlan, int>
    {
        public async Task<int> Handle(CreateSubscriptionPlan request, CancellationToken cancellationToken)
        {
            var plan = new SubscriptionPlan(request.ClubId, request.UserId, request.Name, request.StartDate, request.EndDate, request.PlayersPerWeek);
            context.SubscriptionPlans.Add(plan);

            await context.SaveChangesAsync(cancellationToken);

            return plan.Id;
        }
    }
}
