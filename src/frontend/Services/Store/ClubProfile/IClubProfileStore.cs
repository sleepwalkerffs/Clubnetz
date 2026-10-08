using Bookennis.Client.Services.Store.Base;
using Bookennis.Client.Services.Store.Profile.Models;
using Bookennis.Shared.Controller.ClubProfile;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.ClubProfile;

public interface IClubProfileStore : ISemaphoreStore
{
    event Action OnClubProfileChanged;
    event Action OnBookingOptionsChanged;
    bool IsClubProfileLoaded { get; }
    int SelectedClubId { get; }
    int MemberId { get; }
    MemberRole[] Roles { get; }
    ClubProfileModel? SelectedClub { get; }
    GetBookingOptionsResult? BookingOptions { get; }
    Task LoadClubProfile(int clubId);
    Task LoadBookingOptions();
}