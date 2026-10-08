using Bookennis.Client.Services.HttpClients.Leaderboards;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Leaderboards;

namespace Bookennis.Client.Services.Store.Leaderboards;

public class LeaderboardStore(ILeaderboardHttpClient leaderboardHttpClient) : SemaphoreStore, ILeaderboardStore
{
    public event Action? OnLeaderboardChanged;
    public GetLeaderboardResult? Leaderboard { get; private set; }

    public Task LoadLeaderboard() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await leaderboardHttpClient.GetLeaderboard(cancellationToken);
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
                await LoadLeaderboard();
            }
            return response;
        }, nameof(ToggleOptOut));
        return success;
    }
}
