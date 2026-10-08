using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

/// <summary>Subscription plans are private: only the user who created a plan may access it.</summary>
public class SubscriptionPlanAuthorizationHandler(AppDbContext dbContext)
    : AuthorizationHandler<OwnsSubscriptionPlanRequirement, SubscriptionPlanAuthorizationModel>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnsSubscriptionPlanRequirement requirement, SubscriptionPlanAuthorizationModel resource)
    {
        if (!context.User.IsAuthenticated() || !context.User.TryGetId(out var userId))
            return;

        var isOwner = await dbContext.SubscriptionPlans
            .AnyAsync(plan => plan.Id == resource.SubscriptionPlanId && plan.OwnerUserId == userId);

        if (isOwner)
            context.Succeed(requirement);
    }
}
