using Bookennis.Domain.Base;
using Bookennis.Domain.Courts;

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

    /// <summary>
    /// The sports the order added to the member's enrollment when it was paid, spelled out (never "empty = every sport"): what a
    /// revoke takes back, so sports the treasurer set by hand or another order paid for stay. <c>null</c>: not fulfilled (yet), or
    /// fulfilled before this was recorded.
    /// </summary>
    public Sport[]? AddedSports { get; private set; }

    public void RecordAddedSports(IEnumerable<Sport> sports) => AddedSports = Sports.Normalize((AddedSports ?? []).Concat(sports));
}
