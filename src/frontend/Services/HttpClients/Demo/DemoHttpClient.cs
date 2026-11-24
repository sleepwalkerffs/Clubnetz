using System.Text.Json;
using Bookennis.Shared.Controller.Demo;

namespace Bookennis.Client.Services.HttpClients.Demo;

public class DemoHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IDemoHttpClient
{
    public async Task<HttpResult<GetDemoInfoResult>> GetInfo(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("Demo", cancellationToken)).AsHttpResult<GetDemoInfoResult>(jsonOptions, cancellationToken);
}
