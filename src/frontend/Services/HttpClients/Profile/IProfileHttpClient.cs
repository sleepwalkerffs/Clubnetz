using Bookennis.Shared.Controller.Profile;

namespace Bookennis.Client.Services.HttpClients.Profile;

public interface IProfileHttpClient
{
    public Task<HttpResult<GetUserProfileResult>> GetUserProfile(CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateUserProfile(UpdateUserProfileModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> BecomeClubMember(BecomeClubMemberModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetAvailableClubsResult>> GetAvailableClubs(CancellationToken cancellationToken = default);
    public Task<HttpResult> RequestEmailChange(ChangeEmailModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> ChangePassword(ChangePasswordModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DismissCompletionBanner(CancellationToken cancellationToken = default);
    public Task<HttpResult> LeaveClub(LeaveClubModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<FileDownload>> ExportPersonalData(CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteAccount(DeleteAccountModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> UploadProfilePicture(Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
}
