using System.Text.Json;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.HttpClients.Statistics;

public class StatisticsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IStatisticsHttpClient
{
    public async Task<HttpResult<GetMemberStatisticsResult>> GetMemberStatistics(Sport? sport, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync(sport is null ? "" : $"?sport={sport}", cancellationToken)).AsHttpResult<GetMemberStatisticsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetClubStatisticsResult>> GetClubStatistics(Sport? sport, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync(sport is null ? "club" : $"club?sport={sport}", cancellationToken)).AsHttpResult<GetClubStatisticsResult>(jsonOptions, cancellationToken);
}
