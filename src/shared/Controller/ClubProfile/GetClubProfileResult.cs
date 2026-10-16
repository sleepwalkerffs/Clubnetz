using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.ClubProfile;

public record GetClubProfileResult
{
    public required string ClubName { get; set; }
    public required int MemberId { get; set; }
    public required MemberRole[] Role { get; set; }

    /// <summary>
    /// An application administrator who is not a member of the club: <see cref="MemberId"/> is 0 and everything that
    /// belongs to a member (own bookings, statistics, registrations) is not available.
    /// </summary>
    public bool IsSupportMode { get; set; }

    /// <summary>
    /// The member may see the other members: enrolled in the active season, a parent of an enrolled child, a staff role (anything but
    /// User), or support mode. Members who are not active see the booking calendar anonymized and have no leaderboard,
    /// statistics, season review, subscription planner or profiles and trophy cases of others; they can buy court time.
    /// </summary>
    public bool IsActiveMember { get; set; }
}

