using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

/// <summary>
/// A family belongs to its members: they may read it, its parents may change it, and a member may take themself out of it.
/// Everybody else in the club has no access to it, except the admins of the club.
/// </summary>
public class FamilyAuthorizationHandler(AppDbContext dbContext, ITenantService tenantService, AuthorizationHandlerService handlerService)
    : AuthorizationHandler<FamilyAccessRequirement, FamilyAuthorizationModel>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FamilyAccessRequirement requirement, FamilyAuthorizationModel resource)
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

        if (roles.Contains(MemberRole.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        var membership = await dbContext.Families
            .Where(family => family.Id == resource.FamilyId && family.ClubId == clubId)
            .Select(family => new
            {
                IsParent = family.Parents.Any(parent => parent.MemberId == memberId),
                IsChild = family.Children.Any(child => child.MemberId == memberId),
            })
            .FirstOrDefaultAsync();

        if (membership is null)
            return;

        var isMember = membership.IsParent || membership.IsChild;
        var leavesThemself = isMember && resource.LeavingMemberId == memberId;

        if (membership.IsParent || leavesThemself || (isMember && !requirement.ParentsOnly))
            context.Succeed(requirement);
    }
}
