using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.Store.Statistics;

public interface IStatisticsStore : ISemaphoreStore
{
    public event Action? OnStatisticsChanged;
    public GetMemberStatisticsResult? Statistics { get; }
    /// <summary>Loads the statistics of the sport (<c>null</c> = every sport).</summary>
    public Task LoadStatistics(Sport? sport = null);
}
