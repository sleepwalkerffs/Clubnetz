using System.Security.Claims;
using Bookennis.Api.Business.Permissions;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization;

/// <summary>
/// Resource level of the permissions: the policy lets a member through who has the permission for at least one resource,
/// this checks it for the resources the request really touches (every one of them, otherwise 403).
/// </summary>
public class ResourcePermissionService(AppDbContext dbContext, ITenantService tenantService, AuthorizationHandlerService handlerService)
{
    public async Task EnsureAsync(ClaimsPrincipal user, Permission permission, PermissionResourceType resourceType, IEnumerable<int> resourceIds, CancellationToken cancellationToken)
    {
        // Application administrators pass (support mode), as with every policy
        if (user.IsAdministrator())
            return;

        if (!tenantService.TryGetTenantId(out var clubId) || !user.TryGetId(out var userId))
            throw new AccessDeniedException("No club.");

        var (memberId, roles) = await handlerService.GetMember(userId, clubId, user);
        if (memberId is null || roles is null)
            throw new AccessDeniedException("Not a member of the club.");

        foreach (var resourceId in resourceIds.Distinct())
        {
            if (!await PermissionEvaluator.Has(dbContext, clubId, memberId.Value, roles, permission, resourceType, resourceId, cancellationToken))
                throw new AccessDeniedException($"Missing permission {permission} for {resourceType} {resourceId}.");
        }
    }

    /// <summary>The courts of an existing blocking (and the new ones, if it is changed) are all in the hands of the caller.</summary>
    public async Task EnsureCourtBlockingAsync(ClaimsPrincipal user, IEnumerable<int> newCourtIds, int? courtBlockingId, CancellationToken cancellationToken)
    {
        var courtIds = newCourtIds.ToList();
        if (courtBlockingId is { } id)
            courtIds.AddRange(await dbContext.CourtBlockingCourts.Where(c => c.CourtBlockingId == id).Select(c => c.CourtId).ToListAsync(cancellationToken));

        await EnsureAsync(user, Permission.CourtBlockingsManage, PermissionResourceType.Court, courtIds, cancellationToken);
    }
}
