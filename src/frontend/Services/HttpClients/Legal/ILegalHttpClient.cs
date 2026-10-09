using Bookennis.Shared.Controller.Legal;

namespace Bookennis.Client.Services.HttpClients.Legal;

public interface ILegalHttpClient
{
    public Task<HttpResult<GetLegalSettingsResult>> GetSettings(CancellationToken cancellationToken = default);
}
