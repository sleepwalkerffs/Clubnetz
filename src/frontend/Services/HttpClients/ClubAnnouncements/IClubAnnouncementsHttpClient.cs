using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Client.Services.HttpClients.ClubAnnouncements;

public interface IClubAnnouncementsHttpClient
{
    public Task<HttpResult<GetClubAnnouncementsResult>> GetClubAnnouncements(bool includeExpired, int? take, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubAnnouncementDto>> GetClubAnnouncement(int clubAnnouncementId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> CreateClubAnnouncement(SaveClubAnnouncementModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubAnnouncementDto>> UpdateClubAnnouncement(int clubAnnouncementId, SaveClubAnnouncementModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteClubAnnouncement(int clubAnnouncementId, CancellationToken cancellationToken = default);
    public Task<HttpResult<PreviewClubAnnouncementResult>> PreviewClubAnnouncement(PreviewClubAnnouncementModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubAnnouncementDto>> AddClubAnnouncementAttachment(int clubAnnouncementId, Stream content, string fileName, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubAnnouncementDto>> RemoveClubAnnouncementAttachment(int clubAnnouncementId, int attachmentId, CancellationToken cancellationToken = default);
    public Task<HttpResult<FileDownload>> GetClubAnnouncementAttachment(int clubAnnouncementId, int attachmentId, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubAnnouncementEmailRecipientsResult>> GetClubAnnouncementEmailRecipients(SendClubAnnouncementEmailModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubAnnouncementDto>> SendClubAnnouncementEmail(int clubAnnouncementId, SendClubAnnouncementEmailModel model, CancellationToken cancellationToken = default);
}
