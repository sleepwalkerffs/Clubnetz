using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

/// <summary>
/// Members who are not active (not enrolled in the active season, no enrolled child, no staff role) do not get to see the other members:
/// no leaderboard, statistics, season review, profiles, trophy cases or player lists. Application administrators pass (support mode).
/// </summary>
public class ActiveMemberAuthorizationHandler(AppDbContext dbContext, ITenantService tenantService, AuthorizationHandlerService handlerService)
    : AuthorizationHandler<ActiveMemberRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveMemberRequirement requirement)
    {
        if (!context.User.IsAuthenticated())
            return;

        if (context.User.IsAdministrator())
        {
            context.Succeed(requirement);
            return;
        }

        if (!tenantService.TryGetTenantId(out var clubId) || !context.User.TryGetId(out var userId))
            return;

        var (memberId, _) = await handlerService.GetMember(userId, clubId, context.User);
        if (memberId is null)
            return;

        if (await ActiveMembership.IsActive(dbContext, clubId, memberId.Value, CancellationToken.None))
            context.Succeed(requirement);
    }
}
