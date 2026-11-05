using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

/// <summary>
/// A play request belongs to the member who made it and the members who were asked, nobody else: not other members of the club, not the
/// admins and not an application administrator in support mode (there is no member who would play). Everybody else gets 403 whether the
/// request exists or not.
/// </summary>
public class PlayRequestAuthorizationHandler(AppDbContext dbContext, ITenantService tenantService)
    : AuthorizationHandler<TakesPartInPlayRequestRequirement, PlayRequestAuthorizationModel>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TakesPartInPlayRequestRequirement requirement, PlayRequestAuthorizationModel resource)
    {
        if (!context.User.IsAuthenticated() || !tenantService.TryGetTenantId(out var clubId) || !context.User.TryGetId(out var userId))
            return;

        var takesPart = await dbContext.PlayRequests
            .Where(r => r.Id == resource.PlayRequestId && r.ClubId == clubId)
            .AnyAsync(r =>
                dbContext.ClubMembers.Any(m => m.UserId == userId && m.ClubId == clubId
                    && (m.Id == r.RequesterMemberId || r.Invitees.Any(i => i.MemberId == m.Id))));

        if (takesPart)
            context.Succeed(requirement);
    }
}
