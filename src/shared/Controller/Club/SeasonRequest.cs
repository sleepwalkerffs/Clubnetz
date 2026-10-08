namespace Bookennis.Shared.Controller.Club;

public record SeasonRequest
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
}
