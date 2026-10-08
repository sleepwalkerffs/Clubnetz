using System.Text.Json;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.HttpClients.Statistics;

public class StatisticsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IStatisticsHttpClient
{
    public async Task<HttpResult<GetMemberStatisticsResult>> GetMemberStatistics(CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<GetMemberStatisticsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetClubStatisticsResult>> GetClubStatistics(CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("club", cancellationToken)).AsHttpResult<GetClubStatisticsResult>(jsonOptions, cancellationToken);
}
