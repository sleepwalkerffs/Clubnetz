using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Permissions;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Permissions;

public interface IPermissionsStore : ISemaphoreStore
{
    event Action? OnPermissionsChanged;

    /// <summary>The matrix of the club (what each role may do); <c>null</c> until loaded.</summary>
    GetRolePermissionsResult? Matrix { get; }

    /// <summary>The grants of the club or of the member that was loaded last; <c>null</c> until loaded.</summary>
    List<PermissionGrantDto>? Grants { get; }

    Task LoadMatrix();
    Task<HttpResult> UpdateRole(MemberRole role, IEnumerable<Permission> permissions);
    Task<HttpResult> ResetRole(MemberRole role);
    Task<HttpResult> ResetAll();

    /// <summary>Loads the grants of one member, or of the whole club when <paramref name="memberId"/> is <c>null</c>.</summary>
    Task LoadGrants(int? memberId);
    Task<HttpResult> CreateGrant(CreatePermissionGrantRequest request);
    Task<HttpResult> DeleteGrant(int grantId);
}
