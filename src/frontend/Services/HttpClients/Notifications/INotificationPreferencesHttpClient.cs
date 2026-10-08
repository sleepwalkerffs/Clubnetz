using Bookennis.Shared.Controller.Notifications;

namespace Bookennis.Client.Services.HttpClients.Notifications;

public interface INotificationPreferencesHttpClient
{
    public Task<HttpResult<GetNotificationPreferencesResult>> Get(CancellationToken cancellationToken = default);
    public Task<HttpResult> Update(UpdateNotificationPreferencesModel model, CancellationToken cancellationToken = default);
}
