using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Profile;

namespace Bookennis.Client.Services.HttpClients.Profile;

public class ProfileHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IProfileHttpClient
{
    public async Task<HttpResult<GetUserProfileResult>> GetUserProfile(CancellationToken cancellationToken)
        => await (await httpClient.GetAsync((string?)null, cancellationToken)).AsHttpResult<GetUserProfileResult>(jsonOptions, cancellationToken);
    public async Task<HttpResult> UpdateUserProfile(UpdateUserProfileModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("Update", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult> BecomeClubMember(BecomeClubMemberModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("BecomeClubMember", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult<GetAvailableClubsResult>> GetAvailableClubs(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("AvailableClubs", cancellationToken)).AsHttpResult<GetAvailableClubsResult>(jsonOptions, cancellationToken);
    public async Task<HttpResult> RequestEmailChange(ChangeEmailModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("ChangeEmail", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult> ChangePassword(ChangePasswordModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("ChangePassword", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult> DismissCompletionBanner(CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync("DismissCompletionBanner", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult> LeaveClub(LeaveClubModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("LeaveClub", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult<FileDownload>> ExportPersonalData(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("export", cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult> DeleteAccount(DeleteAccountModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("DeleteAccount", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult> UploadProfilePicture(Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        return await (await httpClient.PostAsync("picture", content, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    }
}