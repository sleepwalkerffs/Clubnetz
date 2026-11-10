using Bookennis.Domain.Base;
using Bookennis.Domain.Members;

namespace Bookennis.Domain.Permissions;

/// <summary>
/// One cell of the role matrix of a club: the <see cref="Role"/> has the <see cref="Permission"/>. A club without any row uses the
/// defaults of <see cref="PermissionCatalog"/>; the first change stores the whole matrix. Unique per club, role and permission.
/// </summary>
#pragma warning disable CA1711 // The domain word is "permission"
public class RolePermission : TenantDomainEntity
#pragma warning restore CA1711
{
#pragma warning disable CS8618
    private RolePermission() { }
#pragma warning restore CS8618

    public RolePermission(int clubId, MemberRole role, Permission permission)
    {
        ClubId = clubId;
        Role = role;
        Permission = permission;
    }

    public MemberRole Role { get; private set; }
    public Permission Permission { get; private set; }
}
