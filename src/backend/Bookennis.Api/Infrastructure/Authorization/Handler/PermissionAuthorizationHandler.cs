using Bookennis.Api.Business.Permissions;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

/// <summary>
/// Passes when the member of the club has the permission: Admin always, otherwise the role matrix of the club and the grants of the member.
/// Application administrators (support mode) pass everything. A caller without a member of the club never passes (403).
/// </summary>
public class PermissionAuthorizationHandler(ITenantService tenantService, AuthorizationHandlerService handlerService, AppDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
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

        var (memberId, roles) = await handlerService.GetMember(userId, clubId, context.User);
        if (memberId is null || roles is null)
            return;

        if (await PermissionEvaluator.Has(dbContext, clubId, memberId.Value, roles, requirement.Permission, null, null, CancellationToken.None))
            context.Succeed(requirement);
    }
}
