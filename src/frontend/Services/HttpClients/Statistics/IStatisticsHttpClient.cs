using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Client.Services.HttpClients.Statistics;

public interface IStatisticsHttpClient
{
    public Task<HttpResult<GetMemberStatisticsResult>> GetMemberStatistics(Sport? sport, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetClubStatisticsResult>> GetClubStatistics(Sport? sport, CancellationToken cancellationToken = default);
}
