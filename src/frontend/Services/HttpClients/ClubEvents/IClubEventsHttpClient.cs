using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Client.Services.HttpClients.ClubEvents;

public interface IClubEventsHttpClient
{
    public Task<HttpResult<GetClubEventsResult>> GetClubEvents(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubEventDto>> GetClubEvent(int clubEventId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> CreateClubEvent(SaveClubEventModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubEventDto>> UpdateClubEvent(int clubEventId, SaveClubEventModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<PreviewClubEventDescriptionResult>> PreviewClubEventDescription(PreviewClubEventDescriptionModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteClubEvent(int clubEventId, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubEventDto>> RegisterForClubEvent(int clubEventId, RegisterForClubEventModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubEventDto>> UnregisterFromClubEvent(int clubEventId, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubEventDto>> RemoveClubEventRegistration(int clubEventId, int registrationId, CancellationToken cancellationToken = default);
    public Task<HttpResult<FileDownload>> ExportClubEventParticipants(int clubEventId, CancellationToken cancellationToken = default);
}
