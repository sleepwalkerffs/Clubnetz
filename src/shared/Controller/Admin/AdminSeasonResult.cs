namespace Bookennis.Shared.Controller.Admin;

public record AdminSeasonResult
{
    public required int Id { get; init; }
    public required int ClubId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsActive { get; init; }
}
