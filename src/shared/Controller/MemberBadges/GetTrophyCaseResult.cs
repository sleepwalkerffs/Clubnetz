using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Shared.Controller.MemberBadges;

public record GetTrophyCaseResult
{
    public required bool IsOwnProfile { get; init; }
    public required bool IsPublic { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public string? DisplayBadgeImageUrl { get; init; }
    public int? DisplayBadgeLevel { get; init; }
    public required List<ClubTrophiesDto> Clubs { get; init; }
}

public record ClubTrophiesDto
{
    public required int ClubId { get; init; }
    public required string ClubName { get; init; }
    public required List<SeasonTrophiesDto> Seasons { get; init; }
}

public record SeasonTrophiesDto
{
    public required int SeasonId { get; init; }
    public required DateOnly SeasonFrom { get; init; }
    public required string SeasonLabel { get; init; }
    public required List<EarnedBadgeDto> Badges { get; init; }
    public required List<OneTimeBadgeAwardDto> OneTimeBadges { get; init; }
}
