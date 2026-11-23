using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.BadgeTiers;

public record GetBadgeTiersResult
{
    public required List<BadgeTierDto> Tiers { get; init; }

    /// <summary>The sports of the club's courts. With more than one, tiers can count the matches of one sport (a track per sport).</summary>
    public List<Sport> Sports { get; init; } = [];
}

public record BadgeTierDto
{
    public required int Id { get; init; }
    public required int Level { get; init; }

    /// <summary>The track of the tier: only matches on courts of this sport count; <c>null</c> = every sport. Levels are numbered per track.</summary>
    public Sport? Sport { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int MatchesRequired { get; init; }
    public required int SortOrder { get; init; }
    public string? ImageUrl { get; init; }

    /// <summary>Number of members that earned this tier.</summary>
    public int EarnedCount { get; init; }
}
