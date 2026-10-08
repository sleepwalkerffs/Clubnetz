using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Members;

public record UpdateMemberRolesRequest
{
    public required MemberRole[] Roles { get; init; }
}
