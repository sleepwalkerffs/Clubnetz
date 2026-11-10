using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Permissions;

/// <summary>
/// The one place that decides whether a member has a permission. Order: Admin always has everything; an explicit deny wins;
/// a grant for the resource or a general grant allows; otherwise the role matrix of the club decides.
/// The matrix is read on every call (two small indexed queries), so a change takes effect at once on every server.
/// </summary>
public static class PermissionEvaluator
{
    /// <summary>
    /// The matrix of the club: what each role may do. A club that never changed it has no rows and gets the defaults.
    /// </summary>
    public static async Task<Dictionary<MemberRole, HashSet<Permission>>> GetRoleMatrix(AppDbContext context, int clubId, CancellationToken cancellationToken)
    {
        var rows = await context.RolePermissions
            .IgnoreQueryFilters()
            .Where(r => r.ClubId == clubId)
            .Select(r => new { r.Role, r.Permission })
            .ToListAsync(cancellationToken);

        var cells = rows.Count == 0
            ? PermissionCatalog.DefaultMatrix().Select(c => (c.Role, c.Permission))
            : rows.Select(r => (r.Role, r.Permission));

        return cells.GroupBy(c => c.Role).ToDictionary(g => g.Key, g => g.Select(c => c.Permission).ToHashSet());
    }

    /// <summary>
    /// Whether the member has the permission. Without a resource it answers "may the member do this at all": a grant limited to one resource
    /// lets the member through the door (<see cref="PermissionInfo.ResourceType"/> must exist), the code that touches the resource checks it again.
    /// </summary>
    public static async Task<bool> Has(
        AppDbContext context, int clubId, int memberId, IReadOnlyCollection<MemberRole> roles, Permission permission,
        PermissionResourceType? resourceType, int? resourceId, CancellationToken cancellationToken)
    {
        if (roles.Contains(MemberRole.Admin))
            return true;

        var grants = await context.PermissionGrants
            .IgnoreQueryFilters()
            .Where(g => g.ClubId == clubId && g.MemberId == memberId && g.Permission == permission)
            .Select(g => new { g.ResourceType, g.ResourceId, g.Effect })
            .ToListAsync(cancellationToken);

        bool Applies(PermissionResourceType? type, int? id) => type is null || (resourceType == type && resourceId == id);

        var applicable = grants.Where(g => Applies(g.ResourceType, g.ResourceId)).ToList();
        if (applicable.Any(g => g.Effect == PermissionEffect.Deny))
            return false;

        if (applicable.Any(g => g.Effect == PermissionEffect.Allow))
            return true;

        // At the door a grant for one resource is enough
        if (resourceType is null && grants.Any(g => g.Effect == PermissionEffect.Allow && g.ResourceType is not null))
            return true;

        var matrix = await GetRoleMatrix(context, clubId, cancellationToken);
        return roles.Any(role => matrix.TryGetValue(role, out var permissions) && permissions.Contains(permission));
    }

    /// <summary>The permissions a member has without looking at a resource (for the app to show or hide things; the server decides).</summary>
    public static async Task<List<Permission>> GetEffective(AppDbContext context, int clubId, int memberId, IReadOnlyCollection<MemberRole> roles, CancellationToken cancellationToken)
    {
        if (roles.Contains(MemberRole.Admin))
            return PermissionCatalog.All.Select(p => p.Permission).ToList();

        var grants = await context.PermissionGrants
            .IgnoreQueryFilters()
            .Where(g => g.ClubId == clubId && g.MemberId == memberId)
            .Select(g => new { g.Permission, g.ResourceType, g.Effect })
            .ToListAsync(cancellationToken);
        var matrix = await GetRoleMatrix(context, clubId, cancellationToken);

        var result = new List<Permission>();
        foreach (var info in PermissionCatalog.All)
        {
            var own = grants.Where(g => g.Permission == info.Permission).ToList();
            if (own.Any(g => g.ResourceType is null && g.Effect == PermissionEffect.Deny))
                continue;

            var allowed = own.Any(g => g.Effect == PermissionEffect.Allow)
                       || roles.Any(role => matrix.TryGetValue(role, out var permissions) && permissions.Contains(info.Permission));
            if (allowed)
                result.Add(info.Permission);
        }

        return result;
    }
}
