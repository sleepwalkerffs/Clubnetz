using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Client.Services.Store.ClubAnnouncements;

public interface IClubAnnouncementsStore : ISemaphoreStore
{
    event Action? OnAnnouncementsChanged;
    event Action? OnAnnouncementChanged;
    event Action? OnLatestAnnouncementsChanged;

    /// <summary>The announcements shown in the news list.</summary>
    List<ClubAnnouncementSummaryDto>? Announcements { get; }

    /// <summary>The announcement currently opened in the detail page or editor.</summary>
    ClubAnnouncementDto? Announcement { get; }

    /// <summary>The newest announcements for the My Club card. Isolated from <see cref="Announcements"/> so the news list can't overwrite it.</summary>
    List<ClubAnnouncementSummaryDto>? LatestAnnouncements { get; }

    Task LoadAnnouncements(bool includeExpired);
    Task LoadLatestAnnouncements(int count);
    Task LoadAnnouncement(int clubAnnouncementId);
    Task<HttpResult<int>> CreateAnnouncement(SaveClubAnnouncementModel model);
    Task<HttpResult> UpdateAnnouncement(int clubAnnouncementId, SaveClubAnnouncementModel model);
    Task<HttpResult> DeleteAnnouncement(int clubAnnouncementId);
    Task<HttpResult<PreviewClubAnnouncementResult>?> Preview(string body);
    Task<HttpResult> AddAttachment(int clubAnnouncementId, byte[] content, string fileName);
    Task<HttpResult> RemoveAttachment(int clubAnnouncementId, int attachmentId);
    Task<HttpResult<FileDownload>> DownloadAttachment(int clubAnnouncementId, int attachmentId);
    Task<HttpResult<ClubAnnouncementEmailRecipientsResult>?> GetEmailRecipients(SendClubAnnouncementEmailModel model);
    Task<HttpResult> SendEmail(int clubAnnouncementId, SendClubAnnouncementEmailModel model);
}
