using Bookennis.Client.Services.HttpClients.Statistics;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.Store.Statistics;

public class ClubStatisticsStore(IStatisticsHttpClient statisticsHttpClient) : SemaphoreStore, IClubStatisticsStore
{
    public event Action? OnClubStatisticsChanged;
    public GetClubStatisticsResult? ClubStatistics { get; private set; }

    public Task LoadClubStatistics() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await statisticsHttpClient.GetClubStatistics(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            ClubStatistics = response.Dto;
            OnClubStatisticsChanged?.Invoke();
        }
    }, nameof(LoadClubStatistics));
}
