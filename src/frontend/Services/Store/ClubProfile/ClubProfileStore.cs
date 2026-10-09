using Bookennis.Client.Services.HttpClients.ClubProfile;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Client.Services.Store.Profile.Models;
using Bookennis.Shared.Controller.ClubProfile;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.ClubProfile;

public class ClubProfileStore(IClubProfileHttpClient clubProfileHttpClient) : SemaphoreStore, IClubProfileStore
{
    public event Action? OnClubProfileChanged;
    public event Action? OnBookingOptionsChanged;
    public bool Loaded { get; private set; }
    public bool IsClubProfileLoaded { get; private set; }
    public int SelectedClubId => SelectedClub?.ClubId ?? throw new ArgumentException("No club selected");
    public int MemberId => SelectedClub?.MemberId ?? throw new ArgumentException("No club selected");
    public MemberRole[] Roles => SelectedClub?.MemberRole ?? throw new ArgumentException("No club selected");
    public ClubProfileModel? SelectedClub { get; private set; }
    public bool IsSupportMode => SelectedClub?.IsSupportMode ?? false;
    public GetBookingOptionsResult? BookingOptions { get; private set; }

    public Task LoadClubProfile(int clubId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            IsClubProfileLoaded = false;
            Loaded = false;
            var memberResponse = await clubProfileHttpClient.GetClubProfile(clubId, cancellationToken);

            if (memberResponse is { Success: true, Dto: not null })
            {
                SelectedClub = new ClubProfileModel
                {
                    ClubId = clubId,
                    MemberId = memberResponse.Dto.MemberId,
                    MemberRole = memberResponse.Dto.Role,
                    ClubName = memberResponse.Dto.ClubName,
                    IsSupportMode = memberResponse.Dto.IsSupportMode
                };

                OnClubProfileChanged?.Invoke();
                IsClubProfileLoaded = true;
                Loaded = true;
            }
        }, nameof(LoadClubProfile));



    public Task LoadBookingOptions()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            IsClubProfileLoaded = false;
            var memberResponse = await clubProfileHttpClient.GetBookingOptions(cancellationToken);

            if (memberResponse is { Success: true, Dto: not null })
            {
                BookingOptions = memberResponse.Dto;
                OnBookingOptionsChanged?.Invoke();
            }
        }, nameof(LoadBookingOptions));
}