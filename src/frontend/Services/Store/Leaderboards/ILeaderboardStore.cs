using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Leaderboards;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Leaderboards;

public interface ILeaderboardStore : ISemaphoreStore
{
    public event Action? OnLeaderboardChanged;
    public GetLeaderboardResult? Leaderboard { get; }
    /// <summary>The sport of the loaded leaderboard; <c>null</c> = every sport.</summary>
    public Sport? Sport { get; }

    /// <summary>Loads the leaderboard of the sport (<c>null</c> = every sport).</summary>
    public Task LoadLeaderboard(Sport? sport = null);
    public Task<bool> ToggleOptOut(int seasonId);
}
