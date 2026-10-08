using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Admin;

public record AdminSeasonRequest
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
}
