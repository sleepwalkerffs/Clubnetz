using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.CourtBlockings;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.CourtBlockings;

namespace Bookennis.Client.Services.Store.CourtBlockings;

public class CourtBlockingsStore(ICourtBlockingsHttpClient httpClient) : SemaphoreStore, ICourtBlockingsStore
{
    public event Action? OnOccurrencesChanged;
    public event Action? OnBlockingsChanged;

    public List<CourtBlockingOccurrenceDto> Occurrences { get; private set; } = [];
    public List<CourtBlockingDto>? Blockings { get; private set; }

    public Task LoadOccurrences(DateOnly dayFrom, DateOnly dayTo) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetCourtBlockingOccurrences(dayFrom, dayTo, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Occurrences = response.Dto.Occurrences;
            OnOccurrencesChanged?.Invoke();
        }
    }, nameof(LoadOccurrences));

    public Task LoadBlockings(bool includePast) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetCourtBlockings(includePast, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Blockings = response.Dto.Blockings;
            OnBlockingsChanged?.Invoke();
        }
    }, nameof(LoadBlockings));

    // A read: a newer request cancels the previous one (null is returned for the cancelled one).
    public async Task<HttpResult<GetCourtBlockingConflictsResult>?> GetConflicts(SaveCourtBlockingModel model)
    {
        HttpResult<GetCourtBlockingConflictsResult>? result = null;
        try
        {
            await RunInLoadingContextAsync(async cancellationToken =>
            {
                result = await httpClient.GetCourtBlockingConflicts(model, cancellationToken);
            }, nameof(GetConflicts));
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return result;
    }

    public Task<HttpResult> CreateBlocking(SaveCourtBlockingModel model)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.CreateCourtBlocking(model, cancellationToken), nameof(CreateBlocking));

    public Task<HttpResult> UpdateBlocking(int courtBlockingId, SaveCourtBlockingModel model)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.UpdateCourtBlocking(courtBlockingId, model, cancellationToken), nameof(UpdateBlocking));

    public Task<HttpResult> DeleteBlocking(int courtBlockingId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.DeleteCourtBlocking(courtBlockingId, cancellationToken), nameof(DeleteBlocking));
}
