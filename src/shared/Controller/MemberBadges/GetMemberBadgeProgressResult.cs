using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Shared.Controller.MemberBadges;

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
}

public record EarnedBadgeDto
{
    public required int MemberBadgeId { get; init; }
    public required int BadgeTierId { get; init; }
    public required int Level { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string? ImageUrl { get; init; }
    public required DateTimeOffset EarnedAt { get; init; }
    public required int SeasonId { get; init; }
    public required string SeasonLabel { get; init; }
    public required DateOnly SeasonFrom { get; init; }
}
