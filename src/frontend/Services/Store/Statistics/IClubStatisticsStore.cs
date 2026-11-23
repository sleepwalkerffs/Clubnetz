using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.Store.Statistics;

public interface IClubStatisticsStore : ISemaphoreStore
{
    public event Action? OnClubStatisticsChanged;
    public GetClubStatisticsResult? ClubStatistics { get; }
    /// <summary>Loads the statistics of the sport (<c>null</c> = every sport).</summary>
    public Task LoadClubStatistics(Sport? sport = null);
}
