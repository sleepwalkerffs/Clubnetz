using Bookennis.Domain.Base;
using Bookennis.Domain.Members.Events;

namespace Bookennis.Domain.Members;

public class MemberSeason : DomainEntity
{
    private MemberSeason() { }

    public MemberSeason(int memberId, int seasonId, int? membershipOrderId = null)
    {
        MemberId = memberId;
        SeasonId = seasonId;
        MembershipOrderId = membershipOrderId;
        AddDomainEvent(new MemberActivatedForSeasonDomainEvent(memberId, seasonId));
    }

    public int MemberId { get; private set; }
    public int SeasonId { get; private set; }
    public bool LeaderboardOptOut { get; set; }

    /// <summary>The membership order that enrolled the member; <c>null</c> when the member was enrolled by hand.</summary>
    public int? MembershipOrderId { get; private set; }
}
