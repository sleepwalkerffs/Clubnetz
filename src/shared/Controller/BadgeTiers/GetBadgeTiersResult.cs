namespace Bookennis.Shared.Controller.BadgeTiers;

public record GetBadgeTiersResult
{
    public required List<BadgeTierDto> Tiers { get; init; }
}

public record BadgeTierDto
{
    public required int Id { get; init; }
    public required int Level { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int MatchesRequired { get; init; }
    public required int SortOrder { get; init; }
    public string? ImageUrl { get; init; }

    /// <summary>Number of members that earned this tier.</summary>
    public int EarnedCount { get; init; }
}
