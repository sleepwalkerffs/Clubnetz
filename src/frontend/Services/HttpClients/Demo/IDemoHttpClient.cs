using Bookennis.Shared.Controller.Demo;

namespace Bookennis.Client.Services.HttpClients.Demo;

public interface IDemoHttpClient
{
    public Task<HttpResult<GetDemoInfoResult>> GetInfo(CancellationToken cancellationToken = default);
}
