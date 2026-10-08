namespace Bookennis.Shared.Controller.Profile;

public record GetAvailableClubsResult
{
    public required List<AvailableClubDto> Clubs { get; init; }
}

public record AvailableClubDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}
