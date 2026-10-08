using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.Store.Statistics;

public interface IStatisticsStore : ISemaphoreStore
{
    public event Action? OnStatisticsChanged;
    public GetMemberStatisticsResult? Statistics { get; }
    public Task LoadStatistics();
}
