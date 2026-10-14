using Bookennis.Shared.Controller.Wrapped;

namespace Bookennis.Client.Services.HttpClients.Wrapped;

public interface IWrappedHttpClient
{
    public Task<HttpResult<GetWrappedSeasonsResult>> GetWrappedSeasons(CancellationToken cancellationToken = default);
    public Task<HttpResult<SeasonWrappedResult>> GetSeasonWrapped(int seasonId, CancellationToken cancellationToken = default);
    public Task<HttpResult> PublishSeasonWrapped(int seasonId, CancellationToken cancellationToken = default);
    public Task<HttpResult> UnpublishSeasonWrapped(int seasonId, CancellationToken cancellationToken = default);
}
