using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Admin;

public record AdminCourtRequest
{
    public required string Name { get; init; }
    public required string Alias { get; init; }
    public required int SortOrder { get; init; }
    public DateTimeOffsetInterval? Inactive { get; init; }
}
