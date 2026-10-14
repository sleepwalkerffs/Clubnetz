using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Profile;

/// <summary>A club the user is a member of, for the club switcher.</summary>
public record ProfileClubDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required MemberRole[] Roles { get; init; }
}
