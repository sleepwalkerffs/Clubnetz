using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Notifications;

namespace Bookennis.Client.Services.HttpClients.Notifications;

public class NotificationPreferencesHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : INotificationPreferencesHttpClient
{
    public async Task<HttpResult<GetNotificationPreferencesResult>> Get(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("NotificationPreferences", cancellationToken)).AsHttpResult<GetNotificationPreferencesResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> Update(UpdateNotificationPreferencesModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync("NotificationPreferences", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
