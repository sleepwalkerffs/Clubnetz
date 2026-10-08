using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Members;
using Fusonic.Extensions.Common.Entities;
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
        if (memberId is null || memberRole is null)
            throw new EntityNotFoundException();

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