using Bookennis.Domain.SubscriptionPlans;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.SubscriptionPlans;

public static class SubscriptionPlanQueryExtensions
{
    public static Task<SubscriptionPlan> GetPlanWithDetails(this IQueryable<SubscriptionPlan> plans, int clubId, int subscriptionPlanId, CancellationToken cancellationToken)
        => plans
            .Include(p => p.Participants)
            .Include(p => p.Assignments)
            .AsSplitQuery()
            .SingleRequiredAsync(p => p.Id == subscriptionPlanId && p.ClubId == clubId, cancellationToken);
}
