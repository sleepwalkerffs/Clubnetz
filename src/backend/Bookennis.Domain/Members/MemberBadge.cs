using Bookennis.Domain.Base;
using Bookennis.Domain.Members.Events;

namespace Bookennis.Domain.Members;

public class MemberBadge : DomainEntity
{
    private MemberBadge() { }

    public MemberBadge(int memberId, int badgeTierId, int seasonId)
    {
        MemberId = memberId;
        BadgeTierId = badgeTierId;
        SeasonId = seasonId;
        EarnedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new MemberBadgeAwardedDomainEvent(memberId, badgeTierId, seasonId));
    }

    public int MemberId { get; private set; }
    public int BadgeTierId { get; private set; }
    public int SeasonId { get; private set; }
    public DateTimeOffset EarnedAt { get; private set; }
}
