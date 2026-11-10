using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Permissions;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.HttpClients.Permissions;

public class PermissionsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IPermissionsHttpClient
{
    public async Task<HttpResult<GetRolePermissionsResult>> GetRolePermissions(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("roles", cancellationToken)).AsHttpResult<GetRolePermissionsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateRolePermissions(MemberRole role, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"roles/{(int)role}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ResetRolePermissions(MemberRole role, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"roles/{(int)role}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ResetAllRolePermissions(CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync("roles", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetPermissionGrantsResult>> GetPermissionGrants(int? memberId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(memberId is { } id ? $"grants?memberId={id}" : "grants", cancellationToken)).AsHttpResult<GetPermissionGrantsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> CreatePermissionGrant(CreatePermissionGrantRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("grants", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeletePermissionGrant(int grantId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"grants/{grantId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
