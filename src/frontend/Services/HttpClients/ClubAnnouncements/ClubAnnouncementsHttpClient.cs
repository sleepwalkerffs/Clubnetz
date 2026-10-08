using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Client.Services.HttpClients.ClubAnnouncements;

public class ClubAnnouncementsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IClubAnnouncementsHttpClient
{
    public async Task<HttpResult<GetClubAnnouncementsResult>> GetClubAnnouncements(bool includeExpired, int? take, CancellationToken cancellationToken = default)
    {
        var url = $"?includeExpired={(includeExpired ? "true" : "false")}";
        if (take.HasValue)
            url += $"&take={take.Value}";

        return await (await httpClient.GetAsync(url, cancellationToken)).AsHttpResult<GetClubAnnouncementsResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<ClubAnnouncementDto>> GetClubAnnouncement(int clubAnnouncementId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{clubAnnouncementId}", cancellationToken)).AsHttpResult<ClubAnnouncementDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateClubAnnouncement(SaveClubAnnouncementModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubAnnouncementDto>> UpdateClubAnnouncement(int clubAnnouncementId, SaveClubAnnouncementModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{clubAnnouncementId}", model, jsonOptions, cancellationToken)).AsHttpResult<ClubAnnouncementDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteClubAnnouncement(int clubAnnouncementId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{clubAnnouncementId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<PreviewClubAnnouncementResult>> PreviewClubAnnouncement(PreviewClubAnnouncementModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("preview", model, jsonOptions, cancellationToken)).AsHttpResult<PreviewClubAnnouncementResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubAnnouncementDto>> AddClubAnnouncementAttachment(int clubAnnouncementId, Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        // The backend derives the content type from the file extension
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(streamContent, "file", fileName);
        return await (await httpClient.PostAsync($"{clubAnnouncementId}/attachments", form, cancellationToken)).AsHttpResult<ClubAnnouncementDto>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<ClubAnnouncementDto>> RemoveClubAnnouncementAttachment(int clubAnnouncementId, int attachmentId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{clubAnnouncementId}/attachments/{attachmentId}", cancellationToken)).AsHttpResult<ClubAnnouncementDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FileDownload>> GetClubAnnouncementAttachment(int clubAnnouncementId, int attachmentId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{clubAnnouncementId}/attachments/{attachmentId}", cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubAnnouncementEmailRecipientsResult>> GetClubAnnouncementEmailRecipients(SendClubAnnouncementEmailModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("email-recipients", model, jsonOptions, cancellationToken)).AsHttpResult<ClubAnnouncementEmailRecipientsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubAnnouncementDto>> SendClubAnnouncementEmail(int clubAnnouncementId, SendClubAnnouncementEmailModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"{clubAnnouncementId}/email", model, jsonOptions, cancellationToken)).AsHttpResult<ClubAnnouncementDto>(jsonOptions, cancellationToken);
}
