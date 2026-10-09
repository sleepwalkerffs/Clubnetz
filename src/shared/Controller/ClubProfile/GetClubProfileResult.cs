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
}

