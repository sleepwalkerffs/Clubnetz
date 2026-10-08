namespace Bookennis.Shared.Controller.MemberBadges;

public record UpdateMemberBadgeSettingsModel
{
    public int? DisplayBadgeId { get; init; }
    public int? DisplayOneTimeBadgeId { get; init; }
    public required bool TrophyCasePublic { get; init; }
}
