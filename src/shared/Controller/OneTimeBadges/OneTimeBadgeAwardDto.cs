namespace Bookennis.Shared.Controller.OneTimeBadges;

public record OneTimeBadgeAwardDto
{
    public required int MemberOneTimeBadgeId { get; init; }
    public required int OneTimeBadgeId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string? ImageUrl { get; init; }
    public required DateTimeOffset AwardedAt { get; init; }
    public required int SeasonId { get; init; }
    public required string SeasonLabel { get; init; }
    public required DateOnly SeasonFrom { get; init; }
}
