using Bookennis.Domain.Base;
using Bookennis.Domain.Members.Events;

namespace Bookennis.Domain.Members;

public class MemberOneTimeBadge : DomainEntity
{
    private MemberOneTimeBadge() { }

    public MemberOneTimeBadge(int memberId, int oneTimeBadgeId)
    {
        MemberId = memberId;
        OneTimeBadgeId = oneTimeBadgeId;
        AwardedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new MemberOneTimeBadgeAwardedDomainEvent(memberId, oneTimeBadgeId));
    }

    public int MemberId { get; private set; }
    public int OneTimeBadgeId { get; private set; }
    public DateTimeOffset AwardedAt { get; private set; }
}
