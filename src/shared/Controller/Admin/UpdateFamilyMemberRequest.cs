using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record UpdateFamilyMemberRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly Birthday { get; init; }
    public required Gender Gender { get; init; }
}
