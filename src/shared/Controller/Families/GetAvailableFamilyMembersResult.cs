namespace Bookennis.Shared.Controller.Families;

public record GetAvailableFamilyMembersResult
{
    public required List<FamilyMemberDto> AvailableMembers { get; init; }
}