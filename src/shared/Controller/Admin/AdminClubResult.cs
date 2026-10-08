namespace Bookennis.Shared.Controller.Admin;

public record AdminClubResult
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int MemberCount { get; init; }
    public required int PlayModeCount { get; init; }
}
