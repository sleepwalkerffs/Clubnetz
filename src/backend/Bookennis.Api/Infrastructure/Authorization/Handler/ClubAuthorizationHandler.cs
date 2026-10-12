using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Members;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

public class ClubAuthorizationHandler(ITenantService tenantService, AuthorizationHandlerService authorizationHandlerService) : AuthorizationHandler<ClubAssignmentRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ClubAssignmentRequirement requirement)
    {
        if (!context.User.IsAuthenticated())
            return;

        if (context.User.IsAdministrator())
        {
            context.Succeed(requirement);
            return;
        }

        if (!tenantService.TryGetTenantId(out var tenantId))
            return;

        var (memberId, memberRole) = await authorizationHandlerService.GetMember(context.User.GetId(), tenantId, context.User);
        // No member of the club: the requirement stays unmet, which is answered with 403
        if (memberId is null || memberRole is null)
            return;

        if (memberRole.Contains(MemberRole.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        if (context.Resource is not null
         && context.Resource is MemberAuthorizationModel resourceModel
         && memberId.Value != resourceModel.MemberId)
        {
            return;
        }

        if (requirement.LimitToMemberRoles.Length == 0)
        {
            context.Succeed(requirement);
            return;
        }

        if (requirement.LimitToMemberRoles.Any(memberRole.Contains))
        {
            context.Succeed(requirement);
            return;
        }
    }
}