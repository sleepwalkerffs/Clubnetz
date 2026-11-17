using Bookennis.Client.Services.HttpClients.Leaderboards;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Leaderboards;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Leaderboards;

public class LeaderboardStore(ILeaderboardHttpClient leaderboardHttpClient) : SemaphoreStore, ILeaderboardStore
{
    public event Action? OnLeaderboardChanged;
    public GetLeaderboardResult? Leaderboard { get; private set; }

    public Sport? Sport { get; private set; }

    public Task LoadLeaderboard(Sport? sport = null) => RunInLoadingContextAsync(async cancellationToken =>
    {
        Sport = sport;
        var response = await leaderboardHttpClient.GetLeaderboard(sport, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Leaderboard = response.Dto;
            OnLeaderboardChanged?.Invoke();
        }
    }, nameof(LoadLeaderboard));

    public async Task<bool> ToggleOptOut(int seasonId)
    {
        var success = false;
        await RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await leaderboardHttpClient.ToggleOptOut(new ToggleLeaderboardOptOutRequest { SeasonId = seasonId }, cancellationToken);
            if (response.Success)
            {
                success = true;
                await LoadLeaderboard(Sport);
            }
            return response;
        }, nameof(ToggleOptOut));
        return success;
    }
}
