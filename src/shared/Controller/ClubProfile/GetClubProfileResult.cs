using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.ClubProfile;

public record GetClubProfileResult
{
    public required string ClubName { get; set; }
    public required int MemberId { get; set; }
    public required MemberRole[] Role { get; set; }
}

