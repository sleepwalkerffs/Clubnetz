namespace Bookennis.Shared.Controller.Families;

public record FamilyDto
{
    public required int FamilyId { get; init; }
    public required List<FamilyParentDto> Parents { get; init; }
    public required List<FamilyChildDto> Children { get; init; }
}