using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.ClubEvents;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Client.Services.Store.ClubEvents;

public class ClubEventsStore(IClubEventsHttpClient httpClient) : SemaphoreStore, IClubEventsStore
{
    /// <summary>How far ahead the My Club card looks for upcoming events.</summary>
    private const int UpcomingRangeInDays = 365;

    public event Action? OnEventsChanged;
    public event Action? OnEventChanged;
    public event Action? OnUpcomingEventsChanged;

    public GetClubEventsResult? Events { get; private set; }
    public ClubEventDto? Event { get; private set; }
    public List<ClubEventSummaryDto>? UpcomingEvents { get; private set; }

    public Task LoadEvents(DateOnly from, DateOnly to) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetClubEvents(from, to, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Events = response.Dto;
            OnEventsChanged?.Invoke();
        }
    }, nameof(LoadEvents));

    public Task LoadUpcomingEvents(int count) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var response = await httpClient.GetClubEvents(today, today.AddDays(UpcomingRangeInDays), cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            UpcomingEvents = response.Dto.Events.Take(count).ToList();
            OnUpcomingEventsChanged?.Invoke();
        }
    }, nameof(LoadUpcomingEvents));

    public Task LoadEvent(int clubEventId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (Event?.Id != clubEventId)
        {
            Event = null;
            OnEventChanged?.Invoke();
        }

        var response = await httpClient.GetClubEvent(clubEventId, cancellationToken);
        if (response is { Success: true, Dto: not null })
            SetEvent(response.Dto);
    }, nameof(LoadEvent));

    public async Task<HttpResult<int>> CreateEvent(SaveClubEventModel model)
        => (HttpResult<int>)await RunInSavingContextAsync(async cancellationToken => await httpClient.CreateClubEvent(model, cancellationToken), nameof(CreateEvent));

    public Task<HttpResult> UpdateEvent(int clubEventId, SaveClubEventModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.UpdateClubEvent(clubEventId, model, cancellationToken);
            if (response is { Success: true, Dto: not null })
                SetEvent(response.Dto);
            return response;
        }, nameof(UpdateEvent));

    public Task<HttpResult> DeleteEvent(int clubEventId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.DeleteClubEvent(clubEventId, cancellationToken);
            if (response.Success && Event?.Id == clubEventId)
            {
                Event = null;
                OnEventChanged?.Invoke();
            }

            return response;
        }, nameof(DeleteEvent));

    // A read: a newer preview request cancels the previous one (null is returned for the cancelled one).
    public async Task<HttpResult<PreviewClubEventDescriptionResult>?> PreviewDescription(string description)
    {
        HttpResult<PreviewClubEventDescriptionResult>? result = null;
        try
        {
            await RunInLoadingContextAsync(async cancellationToken =>
            {
                result = await httpClient.PreviewClubEventDescription(new PreviewClubEventDescriptionModel { Description = description }, cancellationToken);
            }, nameof(PreviewDescription));
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return result;
    }

    public Task<HttpResult> Register(int clubEventId, RegisterForClubEventModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.RegisterForClubEvent(clubEventId, model, cancellationToken);
            if (response is { Success: true, Dto: not null })
                SetEvent(response.Dto);
            return response;
        }, nameof(Register));

    public Task<HttpResult> Unregister(int clubEventId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.UnregisterFromClubEvent(clubEventId, cancellationToken);
            if (response is { Success: true, Dto: not null })
                SetEvent(response.Dto);
            return response;
        }, nameof(Unregister));

    public Task<HttpResult> RemoveRegistration(int clubEventId, int registrationId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.RemoveClubEventRegistration(clubEventId, registrationId, cancellationToken);
            if (response is { Success: true, Dto: not null })
                SetEvent(response.Dto);
            return response;
        }, nameof(RemoveRegistration));

    public async Task<HttpResult<FileDownload>> ExportParticipants(int clubEventId)
        => (HttpResult<FileDownload>)await RunInSavingContextAsync(async cancellationToken => await httpClient.ExportClubEventParticipants(clubEventId, cancellationToken), nameof(ExportParticipants));

    private void SetEvent(ClubEventDto clubEvent)
    {
        Event = clubEvent;
        OnEventChanged?.Invoke();
    }
}
