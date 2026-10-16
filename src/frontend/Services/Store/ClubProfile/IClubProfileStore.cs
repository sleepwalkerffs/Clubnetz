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

    /// <summary>
    /// An application administrator looks after a club without being a member of it: there is no member
    /// (<see cref="MemberId"/> is 0), so everything personal (My Club, own bookings, statistics) is not available.
    /// </summary>
    bool IsSupportMode { get; }

    /// <summary>
    /// The member may see the other members (enrolled in the active season, parent of an enrolled child, staff, or support mode). Members who are not
    /// active see the booking calendar anonymized, buy court time and have no leaderboard, statistics, season review, subscription planner or
    /// profiles of others: the backend answers those with 403.
    /// </summary>
    bool IsActiveMember { get; }
    GetBookingOptionsResult? BookingOptions { get; }
    Task LoadClubProfile(int clubId);
    Task LoadBookingOptions();
}