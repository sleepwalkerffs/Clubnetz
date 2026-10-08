using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Families;

public record FamilyMemberDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}

public record FamilyParentDto : FamilyMemberDto;

public record FamilyChildDto : FamilyMemberDto
{
    public required bool IsOwnedAccount { get; init; }
    public required DateOnly Birthday { get; init; }
    public required Gender Gender { get; init; }
}