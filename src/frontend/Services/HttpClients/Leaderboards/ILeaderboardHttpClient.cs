using Bookennis.Client.Services.HttpClients;
using Bookennis.Shared.Controller.Leaderboards;

namespace Bookennis.Client.Services.HttpClients.Leaderboards;

public interface ILeaderboardHttpClient
{
    public Task<HttpResult<GetLeaderboardResult>> GetLeaderboard(CancellationToken cancellationToken = default);
    public Task<HttpResult> ToggleOptOut(ToggleLeaderboardOptOutRequest request, CancellationToken cancellationToken = default);
}
