using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Leaderboards;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.HttpClients.Leaderboards;

public class LeaderboardHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : ILeaderboardHttpClient
{
    public async Task<HttpResult<GetLeaderboardResult>> GetLeaderboard(Sport? sport, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync(sport is null ? "" : $"?sport={sport}", cancellationToken)).AsHttpResult<GetLeaderboardResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> ToggleOptOut(ToggleLeaderboardOptOutRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("opt-out", request, jsonOptions, cancellationToken);
        return new HttpResult(response.IsSuccessStatusCode, response.StatusCode, null);
    }
}
