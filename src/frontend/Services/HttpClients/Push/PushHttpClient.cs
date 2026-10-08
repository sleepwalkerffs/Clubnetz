using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Push;

namespace Bookennis.Client.Services.HttpClients.Push;

public class PushHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IPushHttpClient
{
    public async Task<HttpResult<GetPushConfigurationResult>> GetConfiguration(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("Configuration", cancellationToken)).AsHttpResult<GetPushConfigurationResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> SaveSubscription(SavePushSubscriptionModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync("Subscription", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteSubscription(DeletePushSubscriptionModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("Unsubscribe", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> SendTest(CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync("Test", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
