namespace Bookennis.Shared.Controller.Club;

public record GetSeasonsResult
{
    public required List<SeasonResult> Seasons { get; init; }
}
