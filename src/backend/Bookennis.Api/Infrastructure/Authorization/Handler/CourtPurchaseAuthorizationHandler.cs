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
/// A court purchase belongs to its buyer. Treasurers and admins of the club look after all purchases, but only the buyer releases
/// the hold of an unpaid purchase (<see cref="CourtPurchaseAuthorizationModel.BuyerOnly"/>). Everybody else in the club has no access to it.
/// </summary>
public class CourtPurchaseAuthorizationHandler(AppDbContext dbContext, ITenantService tenantService, AuthorizationHandlerService handlerService)
    : AuthorizationHandler<OwnsCourtPurchaseRequirement, CourtPurchaseAuthorizationModel>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnsCourtPurchaseRequirement requirement, CourtPurchaseAuthorizationModel resource)
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

        if (!resource.BuyerOnly && (roles.Contains(MemberRole.Admin) || roles.Contains(MemberRole.Treasurer)))
        {
            context.Succeed(requirement);
            return;
        }

        var isBuyer = await dbContext.CourtPurchases
            .AnyAsync(p => p.Id == resource.CourtPurchaseId && p.ClubId == clubId && p.MemberId == memberId);

        if (isBuyer)
            context.Succeed(requirement);
    }
}
