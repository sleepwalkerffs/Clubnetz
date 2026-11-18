using Bookennis.Shared.Controller.OneTimeBadges;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.MemberBadges;

/// <summary>
/// The progress in the active season. The match and badge fields are those of the first track (<see cref="Tracks"/>):
/// the tiers of every sport, or the first sport that has tiers.
/// </summary>
public record GetMemberBadgeProgressResult
{
    public required int MatchCount { get; init; }
    public required int? CurrentBadgeTierId { get; init; }
    public required string? CurrentBadgeName { get; init; }
    public required int? CurrentBadgeLevel { get; init; }
    public required string? CurrentBadgeImageUrl { get; init; }
    public required int? NextBadgeTierId { get; init; }
    public required string? NextBadgeName { get; init; }
    public required int? NextBadgeMatchesRequired { get; init; }
    public required int? NextBadgeLevel { get; init; }
    public required string? NextBadgeImageUrl { get; init; }
    public required List<EarnedBadgeDto> EarnedBadges { get; init; }
    public required List<OneTimeBadgeAwardDto> OneTimeBadges { get; init; }
    public required int? DisplayBadgeId { get; init; }
    public required int? DisplayOneTimeBadgeId { get; init; }
    public required bool TrophyCasePublic { get; init; }

    /// <summary>
    /// One entry per track of tiers in the active season: every sport (<c>Sport = null</c>) and every sport with its own tiers.
    /// One entry (every sport) when the season has no tiers; empty when there is no active season.
    /// </summary>
    public List<BadgeTrackProgressDto> Tracks { get; init; } = [];
}

/// <summary>Progress in one track of tier badges: only matches on courts of <see cref="Sport"/> count (<c>null</c> = every sport).</summary>
public record BadgeTrackProgressDto
{
    public required Sport? Sport { get; init; }
    public required int MatchCount { get; init; }
    public required int? CurrentBadgeTierId { get; init; }
    public required string? CurrentBadgeName { get; init; }
    public required int? CurrentBadgeLevel { get; init; }
    public required string? CurrentBadgeImageUrl { get; init; }
    public required int? NextBadgeTierId { get; init; }
    public required string? NextBadgeName { get; init; }
    public required int? NextBadgeMatchesRequired { get; init; }
    public required int? NextBadgeLevel { get; init; }
    public required string? NextBadgeImageUrl { get; init; }
}

public record EarnedBadgeDto
{
    public required int MemberBadgeId { get; init; }
    public required int BadgeTierId { get; init; }
    public required int Level { get; init; }

    /// <summary>The sport of the tier's track; <c>null</c> = every sport.</summary>
    public Sport? Sport { get; init; }

    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string? ImageUrl { get; init; }
    public required DateTimeOffset EarnedAt { get; init; }
    public required int SeasonId { get; init; }
    public required string SeasonLabel { get; init; }
    public required DateOnly SeasonFrom { get; init; }
}
