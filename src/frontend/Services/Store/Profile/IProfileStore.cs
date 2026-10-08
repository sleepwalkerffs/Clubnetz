using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Profile;

namespace Bookennis.Client.Services.Store.Profile;

public interface IProfileStore : ISemaphoreStore
{
    event Action? OnProfileChanged;
    bool IsProfileLoaded { get; }
    bool HasClubsAssigned { get; }
    IReadOnlyList<int> AvailableClubs { get; }
    string? ProfilePictureUrl { get; }
    bool IsProfileComplete { get; }
    IReadOnlyList<string> MissingProfileFields { get; }
    bool IsProfileBannerDismissed { get; }
    string? Email { get; }
    bool IsGuestSession { get; }
    bool IsClubAssigned(int clubId);
    Task BecomeClubMember(int clubId);
    int FavoriteClubId { get; }
    Task LoadProfileAsync();
    Task<HttpResult<GetAvailableClubsResult>> GetAvailableClubs();
    Task DismissProfileBanner();
    Task<HttpResult> RequestEmailChange(string currentPassword, string newEmail);
    Task<HttpResult> ChangePassword(string currentPassword, string newPassword);
    Task<HttpResult> LeaveClub(int clubId);
    Task<HttpResult<FileDownload>> ExportPersonalData();
    Task<HttpResult> DeleteAccount(string currentPassword);
}