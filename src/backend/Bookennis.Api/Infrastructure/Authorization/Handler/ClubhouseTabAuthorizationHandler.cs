using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

/// <summary>
/// A tab is paid in the app by its guest: the member themself or a parent of a family the member is a child of (the same right that lets
/// a parent book for the children). Nobody else, not the host (who takes cash and card payments) and not an application administrator
/// in support mode: there is no member who would pay.
/// </summary>
public class ClubhouseTabAuthorizationHandler(AppDbContext dbContext, ITenantService tenantService)
    : AuthorizationHandler<PaysClubhouseTabRequirement, ClubhouseTabAuthorizationModel>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PaysClubhouseTabRequirement requirement, ClubhouseTabAuthorizationModel resource)
    {
        if (!context.User.IsAuthenticated() || !tenantService.TryGetTenantId(out var clubId) || !context.User.TryGetId(out var userId))
            return;

        var tabMemberId = await dbContext.ClubhouseTabs
            .Where(t => t.Id == resource.TabId && t.ClubId == clubId)
            .Select(t => t.MemberId)
            .FirstOrDefaultAsync();

        if (tabMemberId is null)
            return;

        var payers = await ActiveMembership.OwnAndChildMemberIds(dbContext, clubId, userId, CancellationToken.None);
        if (payers.Contains(tabMemberId.Value))
            context.Succeed(requirement);
    }
}
