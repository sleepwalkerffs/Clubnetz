using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Club;

public record GetCourtsResult
{
    public required List<CourtResult> Courts { get; init; }
}

public record CourtResult
{
    public required int CulbId { get; init; }
    public required int CourtId { get; init; }
    public required string Name { get; init; }
    public required string Alias { get; init; }
    public required int SortOrder { get; init; }
    public required DateTimeOffsetInterval? Inactive { get; init; }
}
