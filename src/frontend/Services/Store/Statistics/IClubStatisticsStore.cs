using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.Store.Statistics;

public interface IClubStatisticsStore : ISemaphoreStore
{
    public event Action? OnClubStatisticsChanged;
    public GetClubStatisticsResult? ClubStatistics { get; }
    public Task LoadClubStatistics();
}
