using System.Text.Json;
using Bookennis.Shared.Controller.Wrapped;

namespace Bookennis.Client.Services.HttpClients.Wrapped;

public class WrappedHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IWrappedHttpClient
{
    public async Task<HttpResult<GetWrappedSeasonsResult>> GetWrappedSeasons(CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<GetWrappedSeasonsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<SeasonWrappedResult>> GetSeasonWrapped(int seasonId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"{seasonId}", cancellationToken)).AsHttpResult<SeasonWrappedResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> PublishSeasonWrapped(int seasonId, CancellationToken cancellationToken)
        => await (await httpClient.PutAsync($"{seasonId}/publish", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UnpublishSeasonWrapped(int seasonId, CancellationToken cancellationToken)
        => await (await httpClient.DeleteAsync($"{seasonId}/publish", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
