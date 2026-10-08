using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.HttpClients.Statistics;

public interface IStatisticsHttpClient
{
    public Task<HttpResult<GetMemberStatisticsResult>> GetMemberStatistics(CancellationToken cancellationToken = default);
    public Task<HttpResult<GetClubStatisticsResult>> GetClubStatistics(CancellationToken cancellationToken = default);
}
