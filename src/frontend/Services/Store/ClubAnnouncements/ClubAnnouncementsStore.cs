using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.ClubAnnouncements;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Client.Services.Store.ClubAnnouncements;

public class ClubAnnouncementsStore(IClubAnnouncementsHttpClient httpClient) : SemaphoreStore, IClubAnnouncementsStore
{
    public event Action? OnAnnouncementsChanged;
    public event Action? OnAnnouncementChanged;
    public event Action? OnLatestAnnouncementsChanged;

    public List<ClubAnnouncementSummaryDto>? Announcements { get; private set; }
    public ClubAnnouncementDto? Announcement { get; private set; }
    public List<ClubAnnouncementSummaryDto>? LatestAnnouncements { get; private set; }

    public Task LoadAnnouncements(bool includeExpired) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetClubAnnouncements(includeExpired, null, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Announcements = response.Dto.Announcements;
            OnAnnouncementsChanged?.Invoke();
        }
    }, nameof(LoadAnnouncements));

    public Task LoadLatestAnnouncements(int count) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetClubAnnouncements(includeExpired: false, count, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            LatestAnnouncements = response.Dto.Announcements;
            OnLatestAnnouncementsChanged?.Invoke();
        }
    }, nameof(LoadLatestAnnouncements));

    public Task LoadAnnouncement(int clubAnnouncementId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (Announcement?.Id != clubAnnouncementId)
        {
            Announcement = null;
            OnAnnouncementChanged?.Invoke();
        }

        var response = await httpClient.GetClubAnnouncement(clubAnnouncementId, cancellationToken);
        if (response is { Success: true, Dto: not null })
            SetAnnouncement(response.Dto);
    }, nameof(LoadAnnouncement));

    public async Task<HttpResult<int>> CreateAnnouncement(SaveClubAnnouncementModel model)
        => (HttpResult<int>)await RunInSavingContextAsync(async cancellationToken => await httpClient.CreateClubAnnouncement(model, cancellationToken), nameof(CreateAnnouncement));

    public Task<HttpResult> UpdateAnnouncement(int clubAnnouncementId, SaveClubAnnouncementModel model)
        => RunInSavingContextAsync(async cancellationToken => Apply(await httpClient.UpdateClubAnnouncement(clubAnnouncementId, model, cancellationToken)), nameof(UpdateAnnouncement));

    public Task<HttpResult> DeleteAnnouncement(int clubAnnouncementId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.DeleteClubAnnouncement(clubAnnouncementId, cancellationToken);
            if (response.Success)
            {
                if (Announcement?.Id == clubAnnouncementId)
                {
                    Announcement = null;
                    OnAnnouncementChanged?.Invoke();
                }

                // The My Club card must not show the deleted announcement until it is loaded again
                if (LatestAnnouncements?.RemoveAll(a => a.Id == clubAnnouncementId) > 0)
                    OnLatestAnnouncementsChanged?.Invoke();
            }

            return response;
        }, nameof(DeleteAnnouncement));

    // A read: a newer preview request cancels the previous one (null is returned for the cancelled one).
    public async Task<HttpResult<PreviewClubAnnouncementResult>?> Preview(string body)
    {
        HttpResult<PreviewClubAnnouncementResult>? result = null;
        try
        {
            await RunInLoadingContextAsync(async cancellationToken =>
            {
                result = await httpClient.PreviewClubAnnouncement(new PreviewClubAnnouncementModel { Body = body }, cancellationToken);
            }, nameof(Preview));
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return result;
    }

    public Task<HttpResult> AddAttachment(int clubAnnouncementId, byte[] content, string fileName)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            using var stream = new MemoryStream(content, writable: false);
            return Apply(await httpClient.AddClubAnnouncementAttachment(clubAnnouncementId, stream, fileName, cancellationToken));
        }, nameof(AddAttachment));

    public Task<HttpResult> RemoveAttachment(int clubAnnouncementId, int attachmentId)
        => RunInSavingContextAsync(async cancellationToken => Apply(await httpClient.RemoveClubAnnouncementAttachment(clubAnnouncementId, attachmentId, cancellationToken)), nameof(RemoveAttachment));

    // A read with a context per attachment, so several files can be downloaded side by side.
    public async Task<HttpResult<FileDownload>> DownloadAttachment(int clubAnnouncementId, int attachmentId)
    {
        HttpResult<FileDownload>? result = null;
        await RunInLoadingContextAsync(async cancellationToken =>
        {
            result = await httpClient.GetClubAnnouncementAttachment(clubAnnouncementId, attachmentId, cancellationToken);
        }, $"{nameof(DownloadAttachment)}-{attachmentId}");
        return result!;
    }

    // A read: changing the audience cancels the previous count (null is returned for the cancelled one).
    public async Task<HttpResult<ClubAnnouncementEmailRecipientsResult>?> GetEmailRecipients(SendClubAnnouncementEmailModel model)
    {
        HttpResult<ClubAnnouncementEmailRecipientsResult>? result = null;
        try
        {
            await RunInLoadingContextAsync(async cancellationToken =>
            {
                result = await httpClient.GetClubAnnouncementEmailRecipients(model, cancellationToken);
            }, nameof(GetEmailRecipients));
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return result;
    }

    public Task<HttpResult> SendEmail(int clubAnnouncementId, SendClubAnnouncementEmailModel model)
        => RunInSavingContextAsync(async cancellationToken => Apply(await httpClient.SendClubAnnouncementEmail(clubAnnouncementId, model, cancellationToken)), nameof(SendEmail));

    private HttpResult<ClubAnnouncementDto> Apply(HttpResult<ClubAnnouncementDto> response)
    {
        if (response is { Success: true, Dto: not null })
            SetAnnouncement(response.Dto);
        return response;
    }

    private void SetAnnouncement(ClubAnnouncementDto announcement)
    {
        Announcement = announcement;
        OnAnnouncementChanged?.Invoke();
    }
}
