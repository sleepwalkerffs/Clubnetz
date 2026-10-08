using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record AdminClubMemberResult
{
    public required int MemberId { get; init; }
    public required int UserId { get; init; }
    public required string FullName { get; init; }
    public required string? Email { get; init; }
    public required MemberRole[] Roles { get; init; }
    public required int[] AllowedSeasonIds { get; init; }
    public required int? BookingsPerWeek { get; init; }
}
