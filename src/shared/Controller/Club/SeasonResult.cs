namespace Bookennis.Shared.Controller.Club;

public record SeasonResult
{
    public required int Id { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsActive { get; init; }
}
