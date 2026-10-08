using Bookennis.Shared.Controller.Push;

namespace Bookennis.Client.Services.HttpClients.Push;

public interface IPushHttpClient
{
    public Task<HttpResult<GetPushConfigurationResult>> GetConfiguration(CancellationToken cancellationToken = default);
    public Task<HttpResult> SaveSubscription(SavePushSubscriptionModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteSubscription(DeletePushSubscriptionModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> SendTest(CancellationToken cancellationToken = default);
}
