using System.Text.Json;
using Bookennis.Shared.Controller.Legal;

namespace Bookennis.Client.Services.HttpClients.Legal;

public class LegalHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : ILegalHttpClient
{
    public async Task<HttpResult<GetLegalSettingsResult>> GetSettings(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("Legal", cancellationToken)).AsHttpResult<GetLegalSettingsResult>(jsonOptions, cancellationToken);
}
