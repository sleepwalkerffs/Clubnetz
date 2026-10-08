using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Client.Services.HttpClients.SubscriptionPlans;

public class SubscriptionPlansHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : ISubscriptionPlansHttpClient
{
    public async Task<HttpResult<GetSubscriptionPlansResult>> GetSubscriptionPlans(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<GetSubscriptionPlansResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<SubscriptionPlanDto>> GetSubscriptionPlan(int subscriptionPlanId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{subscriptionPlanId}", cancellationToken)).AsHttpResult<SubscriptionPlanDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateSubscriptionPlan(CreateSubscriptionPlanModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult<SubscriptionPlanDto>> UpdateSubscriptionPlan(int subscriptionPlanId, UpdateSubscriptionPlanModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{subscriptionPlanId}", model, jsonOptions, cancellationToken)).AsHttpResult<SubscriptionPlanDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteSubscriptionPlan(int subscriptionPlanId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{subscriptionPlanId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<SubscriptionPlanDto>> ComputeSubscriptionSchedule(int subscriptionPlanId, ComputeSubscriptionScheduleModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"{subscriptionPlanId}/compute", model, jsonOptions, cancellationToken)).AsHttpResult<SubscriptionPlanDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<SubscriptionPlanDto>> UpdateSubscriptionSchedule(int subscriptionPlanId, UpdateSubscriptionScheduleModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{subscriptionPlanId}/schedule", model, jsonOptions, cancellationToken)).AsHttpResult<SubscriptionPlanDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FileDownload>> ExportSubscriptionPlan(int subscriptionPlanId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{subscriptionPlanId}/export", cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);
}
