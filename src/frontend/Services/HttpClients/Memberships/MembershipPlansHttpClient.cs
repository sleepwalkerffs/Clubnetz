using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public class MembershipPlansHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IMembershipPlansHttpClient
{
    public async Task<HttpResult<GetMembershipPlansResult>> GetMembershipPlans(int seasonId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"?seasonId={seasonId}", cancellationToken)).AsHttpResult<GetMembershipPlansResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateMembershipPlan(int seasonId, SaveMembershipPlanModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"?seasonId={seasonId}", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateMembershipPlan(int planId, SaveMembershipPlanModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{planId}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteMembershipPlan(int planId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{planId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> CopyMembershipPlans(CopyMembershipPlansModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("copy", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> SortMembershipPlans(SortMembershipPlansModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync("sort", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
