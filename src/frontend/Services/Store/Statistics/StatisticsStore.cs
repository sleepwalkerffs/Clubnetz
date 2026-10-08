using Bookennis.Client.Services.HttpClients.Statistics;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.Store.Statistics;

public class StatisticsStore(IStatisticsHttpClient statisticsHttpClient) : SemaphoreStore, IStatisticsStore
{
    public event Action? OnStatisticsChanged;
    public GetMemberStatisticsResult? Statistics { get; private set; }

    public Task LoadStatistics() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await statisticsHttpClient.GetMemberStatistics(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Statistics = response.Dto;
            OnStatisticsChanged?.Invoke();
        }
    }, nameof(LoadStatistics));
}
