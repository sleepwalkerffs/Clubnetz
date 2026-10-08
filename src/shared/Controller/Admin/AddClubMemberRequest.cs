using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record AddClubMemberRequest
{
    public required int UserId { get; init; }
    public required MemberRole[] Roles { get; init; }
}
