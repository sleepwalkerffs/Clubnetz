namespace Bookennis.Shared.Controller.OneTimeBadges;

public record GetOneTimeBadgesResult
{
    public required List<OneTimeBadgeDto> Badges { get; init; }
}

public record OneTimeBadgeDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? ImageUrl { get; init; }
    public required List<OneTimeBadgeAwardeeDto> Awardees { get; init; }
}

public record OneTimeBadgeAwardeeDto
{
    public required int MemberOneTimeBadgeId { get; init; }
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateTimeOffset AwardedAt { get; init; }
}
