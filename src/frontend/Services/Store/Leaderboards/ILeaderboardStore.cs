using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Leaderboards;

namespace Bookennis.Client.Services.Store.Leaderboards;

public interface ILeaderboardStore : ISemaphoreStore
{
    public event Action? OnLeaderboardChanged;
    public GetLeaderboardResult? Leaderboard { get; }
    public Task LoadLeaderboard();
    public Task<bool> ToggleOptOut(int seasonId);
}
