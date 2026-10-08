using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Shared.Controller.MemberBadges;

public record GetMemberBadgesResult
{
    public required List<EarnedBadgeDto> Badges { get; init; }
    public required List<OneTimeBadgeAwardDto> OneTimeBadges { get; init; }
}
