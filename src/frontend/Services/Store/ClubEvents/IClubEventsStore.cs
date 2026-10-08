using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Client.Services.Store.ClubEvents;

public interface IClubEventsStore : ISemaphoreStore
{
    event Action? OnEventsChanged;
    event Action? OnEventChanged;
    event Action? OnUpcomingEventsChanged;

    /// <summary>Events of the range currently shown in the calendar.</summary>
    GetClubEventsResult? Events { get; }

    /// <summary>The event currently opened in the detail page or editor.</summary>
    ClubEventDto? Event { get; }

    /// <summary>The next events for the My Club card. Isolated from <see cref="Events"/> so the calendar can't overwrite it.</summary>
    List<ClubEventSummaryDto>? UpcomingEvents { get; }

    Task LoadEvents(DateOnly from, DateOnly to);
    Task LoadUpcomingEvents(int count);
    Task LoadEvent(int clubEventId);
    Task<HttpResult<int>> CreateEvent(SaveClubEventModel model);
    Task<HttpResult> UpdateEvent(int clubEventId, SaveClubEventModel model);
    Task<HttpResult> DeleteEvent(int clubEventId);

    /// <summary>Renders the Markdown of a description. Null if a newer preview request replaced this one.</summary>
    Task<HttpResult<PreviewClubEventDescriptionResult>?> PreviewDescription(string description);

    Task<HttpResult> Register(int clubEventId, RegisterForClubEventModel model);
    Task<HttpResult> Unregister(int clubEventId);
    Task<HttpResult> RemoveRegistration(int clubEventId, int registrationId);
    Task<HttpResult<FileDownload>> ExportParticipants(int clubEventId);
}
