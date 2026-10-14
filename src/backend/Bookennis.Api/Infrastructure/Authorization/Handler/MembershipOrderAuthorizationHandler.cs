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
/// A membership order belongs to the people it is about: who pays it, the members it covers and the parents of a covered child.
/// Treasurers and admins of the club look after all orders. Everybody else in the club has no access to it.
/// </summary>
public class MembershipOrderAuthorizationHandler(AppDbContext dbContext, ITenantService tenantService, AuthorizationHandlerService handlerService)
    : AuthorizationHandler<OwnsMembershipOrderRequirement, MembershipOrderAuthorizationModel>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnsMembershipOrderRequirement requirement, MembershipOrderAuthorizationModel resource)
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

        if (roles.Contains(MemberRole.Admin) || roles.Contains(MemberRole.Treasurer))
        {
            context.Succeed(requirement);
            return;
        }

        var order = await dbContext.MembershipOrders
            .Where(o => o.Id == resource.MembershipOrderId && o.ClubId == clubId)
            .Select(o => new { o.PayerMemberId, MemberIds = o.Members.Select(m => m.MemberId).ToList() })
            .FirstOrDefaultAsync();

        if (order is null)
            return;

        if (order.PayerMemberId == memberId || order.MemberIds.Contains(memberId.Value))
        {
            context.Succeed(requirement);
            return;
        }

        var isParentOfCoveredChild = await dbContext.Families
            .AnyAsync(family => family.ClubId == clubId
                             && family.Parents.Any(parent => parent.MemberId == memberId)
                             && family.Children.Any(child => order.MemberIds.Contains(child.MemberId)));

        if (isParentOfCoveredChild)
            context.Succeed(requirement);
    }
}
