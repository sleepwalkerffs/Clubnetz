using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record UpdateClubMemberRequest
{
    public required MemberRole[] Roles { get; init; }
    public required int[] AllowedSeasonIds { get; init; }
    public required int BookingsPerWeek { get; init; }
}
