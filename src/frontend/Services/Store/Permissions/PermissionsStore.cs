using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Permissions;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Permissions;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Permissions;

public class PermissionsStore(IPermissionsHttpClient httpClient) : SemaphoreStore, IPermissionsStore
{
    public event Action? OnPermissionsChanged;

    public GetRolePermissionsResult? Matrix { get; private set; }
    public List<PermissionGrantDto>? Grants { get; private set; }

    public Task LoadMatrix() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetRolePermissions(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Matrix = response.Dto;
            OnPermissionsChanged?.Invoke();
        }
    }, nameof(LoadMatrix));

    public Task<HttpResult> UpdateRole(MemberRole role, IEnumerable<Permission> permissions)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.UpdateRolePermissions(role, new UpdateRolePermissionsRequest { Permissions = permissions.ToList() }, cancellationToken), nameof(UpdateRole));

    public Task<HttpResult> ResetRole(MemberRole role)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.ResetRolePermissions(role, cancellationToken), nameof(ResetRole));

    public Task<HttpResult> ResetAll()
        => RunInSavingContextAsync(async cancellationToken => await httpClient.ResetAllRolePermissions(cancellationToken), nameof(ResetAll));

    public Task LoadGrants(int? memberId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetPermissionGrants(memberId, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Grants = response.Dto.Grants;
            OnPermissionsChanged?.Invoke();
        }
    }, nameof(LoadGrants));

    public Task<HttpResult> CreateGrant(CreatePermissionGrantRequest request)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.CreatePermissionGrant(request, cancellationToken), nameof(CreateGrant));

    public Task<HttpResult> DeleteGrant(int grantId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.DeletePermissionGrant(grantId, cancellationToken), nameof(DeleteGrant));
}
