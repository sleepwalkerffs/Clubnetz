using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.Permissions;
using Bookennis.Shared.Controller.Permissions;
using Microsoft.EntityFrameworkCore;
using DomainPermission = Bookennis.Domain.Permissions.Permission;
using SharedPermission = Bookennis.Shared.Controller.Permissions.Permission;
using SharedRole = Bookennis.Shared.Controller.Shared.MemberRole;

namespace Bookennis.Api.Business.Permissions;

/// <summary>The role matrix of the club: what each role may do, and what it gets by default. Admin is not listed, it always has everything.</summary>
public record GetRolePermissions(int ClubId) : IQuery<GetRolePermissionsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetRolePermissions, GetRolePermissionsResult>
    {
        public async Task<GetRolePermissionsResult> Handle(GetRolePermissions request, CancellationToken cancellationToken)
        {
            var matrix = await PermissionEvaluator.GetRoleMatrix(context, request.ClubId, cancellationToken);
            var defaults = PermissionCatalog.DefaultMatrix().GroupBy(c => c.Role).ToDictionary(g => g.Key, g => g.Select(c => c.Permission).ToHashSet());

            return new GetRolePermissionsResult
            {
                Catalog = PermissionCatalog.All.Select(p => new PermissionDto
                {
                    Permission = (SharedPermission)p.Permission,
                    Area = (Bookennis.Shared.Controller.Permissions.PermissionArea)p.Area,
                    IsMoney = p.IsMoney,
                    ResourceType = p.ResourceType is { } type ? (Bookennis.Shared.Controller.Permissions.PermissionResourceType)type : null,
                }).ToList(),
                Roles = PermissionCatalog.EditableRoles.Select(role => new RolePermissionsDto
                {
                    Role = (SharedRole)role,
                    Permissions = Order(matrix.GetValueOrDefault(role)),
                    DefaultPermissions = Order(defaults.GetValueOrDefault(role)),
                }).ToList(),
            };
        }

        private static List<SharedPermission> Order(IEnumerable<DomainPermission>? permissions)
            => (permissions ?? []).OrderBy(p => (int)p).Select(p => (SharedPermission)p).ToList();
    }
}

/// <summary>Sets what one role may do. The first change stores the whole matrix of the club (until then it uses the defaults).</summary>
[SecurityAudit]
public record UpdateRolePermissions(int ClubId, MemberRole Role, IReadOnlyCollection<DomainPermission> Permissions) : ICommand
{
    public enum ErrorCode
    {
        PermissionRoleNotEditable = 0,
    }

    public class Handler(AppDbContext context) : AsyncRequestHandler<UpdateRolePermissions>
    {
        protected override async Task Handle(UpdateRolePermissions request, CancellationToken cancellationToken)
        {
            if (!PermissionCatalog.IsEditableRole(request.Role))
                throw new PreconditionException(ErrorCode.PermissionRoleNotEditable, "The permissions of this role can't be changed.");

            await RolePermissionStorage.Replace(context, request.ClubId, request.Role, request.Permissions.Distinct(), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Back to the defaults: one role, or the whole matrix when no role is given.</summary>
[SecurityAudit]
public record ResetRolePermissions(int ClubId, MemberRole? Role) : ICommand
{
    public class Handler(AppDbContext context) : AsyncRequestHandler<ResetRolePermissions>
    {
        protected override async Task Handle(ResetRolePermissions request, CancellationToken cancellationToken)
        {
            if (request.Role is null)
            {
                var all = await context.RolePermissions.IgnoreQueryFilters().Where(r => r.ClubId == request.ClubId).ToListAsync(cancellationToken);
                context.RolePermissions.RemoveRange(all);
            }
            else
            {
                if (!PermissionCatalog.IsEditableRole(request.Role.Value))
                    throw new PreconditionException(UpdateRolePermissions.ErrorCode.PermissionRoleNotEditable, "The permissions of this role can't be changed.");

                var defaults = PermissionCatalog.DefaultMatrix().Where(c => c.Role == request.Role.Value).Select(c => c.Permission);
                await RolePermissionStorage.Replace(context, request.ClubId, request.Role.Value, defaults, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

internal static class RolePermissionStorage
{
    public static async Task Replace(AppDbContext context, int clubId, MemberRole role, IEnumerable<DomainPermission> permissions, CancellationToken cancellationToken)
    {
        var rows = await context.RolePermissions.IgnoreQueryFilters().Where(r => r.ClubId == clubId).ToListAsync(cancellationToken);

        // A club that still uses the defaults gets them stored first, so only this role changes
        if (rows.Count == 0)
        {
            foreach (var (defaultRole, permission) in PermissionCatalog.DefaultMatrix())
            {
                var row = new RolePermission(clubId, defaultRole, permission);
                context.RolePermissions.Add(row);
                rows.Add(row);
            }
        }

        var wanted = permissions.ToHashSet();
        foreach (var row in rows.Where(r => r.Role == role).ToList())
        {
            if (!wanted.Remove(row.Permission))
                context.RolePermissions.Remove(row);
        }

        foreach (var permission in wanted)
            context.RolePermissions.Add(new RolePermission(clubId, role, permission));
    }
}
