using Bookennis.Shared.Controller.Permissions;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.HttpClients.Permissions;

public interface IPermissionsHttpClient
{
    public Task<HttpResult<GetRolePermissionsResult>> GetRolePermissions(CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateRolePermissions(MemberRole role, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> ResetRolePermissions(MemberRole role, CancellationToken cancellationToken = default);
    public Task<HttpResult> ResetAllRolePermissions(CancellationToken cancellationToken = default);
    public Task<HttpResult<GetPermissionGrantsResult>> GetPermissionGrants(int? memberId, CancellationToken cancellationToken = default);
    public Task<HttpResult> CreatePermissionGrant(CreatePermissionGrantRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeletePermissionGrant(int grantId, CancellationToken cancellationToken = default);
}
