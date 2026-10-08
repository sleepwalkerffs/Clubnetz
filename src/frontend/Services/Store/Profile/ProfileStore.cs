using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Profile;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Profile;

namespace Bookennis.Client.Services.Store.Profile;

public sealed class ProfileStore(IProfileHttpClient profileHttpClient) : SemaphoreStore, IProfileStore
{
    private List<int> availableClubIds = [];
    private List<string> missingProfileFields = [];

    public event Action? OnProfileChanged;

    public bool Loaded { get; private set; }
    public bool IsProfileLoaded { get; private set; }
    public bool HasClubsAssigned => availableClubIds.Count != 0;
    public bool IsClubAssigned(int clubId) => availableClubIds.Contains(clubId);
    public int FavoriteClubId { get; private set; }
    public string? ProfilePictureUrl { get; private set; }
    public bool IsProfileComplete => missingProfileFields.Count == 0;
    public IReadOnlyList<string> MissingProfileFields => missingProfileFields;
    public bool IsProfileBannerDismissed { get; private set; }
    public string? Email { get; private set; }
    public bool IsGuestSession { get; private set; }

    public IReadOnlyList<int> AvailableClubs => availableClubIds;

    public Task LoadProfileAsync()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            Loaded = false;
            var profileResponse = await profileHttpClient.GetUserProfile(cancellationToken);

            if (profileResponse is { Success: true, Dto: not null })
            {
                availableClubIds = profileResponse.Dto.AvailableClubIds;
                FavoriteClubId = profileResponse.Dto.FavoriteClubId;
                ProfilePictureUrl = profileResponse.Dto.ProfilePictureUrl is not null
                    ? $"{profileResponse.Dto.ProfilePictureUrl}?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
                    : null;
                missingProfileFields = ComputeMissingFields(profileResponse.Dto);
                IsProfileBannerDismissed = profileResponse.Dto.ProfileCompletionBannerDismissed;
                Email = profileResponse.Dto.Email;
                IsGuestSession = profileResponse.Dto.IsGuestSession;
                IsProfileLoaded = true;
                Loaded = true;
                OnProfileChanged?.Invoke();
            }
        }, nameof(LoadProfileAsync));

    public Task BecomeClubMember(int clubId)
        => RunInSavingContextAsync(async cancellationToken
            => await profileHttpClient.BecomeClubMember(new BecomeClubMemberModel { ClubId = clubId }, cancellationToken), nameof(BecomeClubMember));

    public async Task<HttpResult<GetAvailableClubsResult>> GetAvailableClubs()
        => await profileHttpClient.GetAvailableClubs();

    public Task DismissProfileBanner()
        => RunInSavingContextAsync(async cancellationToken =>
        {
            // Hide it right away, the request only has to persist the choice
            IsProfileBannerDismissed = true;
            OnProfileChanged?.Invoke();
            await profileHttpClient.DismissCompletionBanner(cancellationToken);
        }, nameof(DismissProfileBanner));

    public Task<HttpResult> RequestEmailChange(string currentPassword, string newEmail)
        => RunInSavingContextAsync(cancellationToken
            => profileHttpClient.RequestEmailChange(new ChangeEmailModel(currentPassword, newEmail), cancellationToken), nameof(RequestEmailChange));

    public Task<HttpResult> ChangePassword(string currentPassword, string newPassword)
        => RunInSavingContextAsync(cancellationToken
            => profileHttpClient.ChangePassword(new ChangePasswordModel(currentPassword, newPassword), cancellationToken), nameof(ChangePassword));

    public Task<HttpResult> LeaveClub(int clubId)
        => RunInSavingContextAsync(cancellationToken
            => profileHttpClient.LeaveClub(new LeaveClubModel { ClubId = clubId }, cancellationToken), nameof(LeaveClub));

    public async Task<HttpResult<FileDownload>> ExportPersonalData()
        => (HttpResult<FileDownload>)await RunInSavingContextAsync(async cancellationToken
            => await profileHttpClient.ExportPersonalData(cancellationToken), nameof(ExportPersonalData));

    public Task<HttpResult> DeleteAccount(string currentPassword)
        => RunInSavingContextAsync(cancellationToken
            => profileHttpClient.DeleteAccount(new DeleteAccountModel(currentPassword), cancellationToken), nameof(DeleteAccount));

    private static List<string> ComputeMissingFields(GetUserProfileResult profile)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.FirstName))
            missing.Add("ProfileMissing_FirstName");
        if (string.IsNullOrWhiteSpace(profile.LastName))
            missing.Add("ProfileMissing_LastName");
        if (profile.Birthday == default)
            missing.Add("ProfileMissing_Birthday");
        if (profile.ProfilePictureUrl is null)
            missing.Add("ProfileMissing_ProfilePicture");

        return missing;
    }
}