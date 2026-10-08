namespace Bookennis.Shared.Controller.Families;

public record CreateFamilyRequest
{
    public List<int> ParentContractUserIds { get; init; } = new();
    public List<int> ChildrenUserContractIds { get; init; } = new();
    public List<AddNewChildRequest> NewChildUsers { get; init; } = new();
}