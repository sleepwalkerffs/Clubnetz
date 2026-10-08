using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Admin;

public record AdminCourtResult
{
    public required int CourtId { get; init; }
    public required int ClubId { get; init; }
    public required string Name { get; init; }
    public required string Alias { get; init; }
    public required int SortOrder { get; init; }
    public required DateTimeOffsetInterval? Inactive { get; init; }
}
