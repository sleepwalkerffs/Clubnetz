using Bookennis.Shared.Controller.CourtBlockings;

namespace Bookennis.Client.Services.HttpClients.CourtBlockings;

public interface ICourtBlockingsHttpClient
{
    public Task<HttpResult<GetCourtBlockingOccurrencesResult>> GetCourtBlockingOccurrences(DateOnly dayFrom, DateOnly dayTo, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetCourtBlockingsResult>> GetCourtBlockings(bool includePast, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetCourtBlockingConflictsResult>> GetCourtBlockingConflicts(SaveCourtBlockingModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> CreateCourtBlocking(SaveCourtBlockingModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateCourtBlocking(int courtBlockingId, SaveCourtBlockingModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteCourtBlocking(int courtBlockingId, CancellationToken cancellationToken = default);
}
