using Bookennis.Domain.Base;

namespace Bookennis.Domain.Memberships;

/// <summary>A member covered by a <see cref="MembershipOrder"/>.</summary>
public class MembershipOrderMember : DomainEntity
{
#pragma warning disable CS8618
    private MembershipOrderMember() { }
#pragma warning restore CS8618

    public MembershipOrderMember(MembershipOrder order, int memberId)
    {
        Order = order;
        MemberId = memberId;
    }

    public int MembershipOrderId { get; private set; }
    public MembershipOrder Order { get; private set; }
    public int MemberId { get; private set; }
}
