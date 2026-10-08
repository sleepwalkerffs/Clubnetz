using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.CourtBlockings;

namespace Bookennis.Client.Services.Store.CourtBlockings;

public interface ICourtBlockingsStore : ISemaphoreStore
{
    event Action? OnOccurrencesChanged;
    event Action? OnBlockingsChanged;

    /// <summary>The blocked time windows of the days currently shown in the booking grid.</summary>
    List<CourtBlockingOccurrenceDto> Occurrences { get; }

    /// <summary>The blockings of the management page; <c>null</c> until they were loaded.</summary>
    List<CourtBlockingDto>? Blockings { get; }

    Task LoadOccurrences(DateOnly dayFrom, DateOnly dayTo);
    Task LoadBlockings(bool includePast);

    /// <summary>The upcoming bookings that saving the blocking would delete. Null if a newer request replaced this one.</summary>
    Task<HttpResult<GetCourtBlockingConflictsResult>?> GetConflicts(SaveCourtBlockingModel model);

    Task<HttpResult> CreateBlocking(SaveCourtBlockingModel model);
    Task<HttpResult> UpdateBlocking(int courtBlockingId, SaveCourtBlockingModel model);
    Task<HttpResult> DeleteBlocking(int courtBlockingId);
}
