using Bookennis.Shared.Controller.Families;

namespace Bookennis.Shared.Controller.Admin;

public record AdminFamilyResult
{
    public required int FamilyId { get; init; }
    public required List<FamilyParentDto> Parents { get; init; }
    public required List<FamilyChildDto> Children { get; init; }
}
