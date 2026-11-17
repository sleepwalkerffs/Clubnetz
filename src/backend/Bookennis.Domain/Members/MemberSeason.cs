using Bookennis.Domain.Base;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members.Events;

namespace Bookennis.Domain.Members;

public class MemberSeason : DomainEntity
{
    private MemberSeason() { }

    public MemberSeason(int memberId, int seasonId, int? membershipOrderId = null, IEnumerable<Sport>? sports = null)
    {
        MemberId = memberId;
        SeasonId = seasonId;
        MembershipOrderId = membershipOrderId;
        Sports = Courts.Sports.Normalize(sports);
        AddDomainEvent(new MemberActivatedForSeasonDomainEvent(memberId, seasonId));
    }

    public int MemberId { get; private set; }
    public int SeasonId { get; private set; }
    public bool LeaderboardOptOut { get; set; }

    /// <summary>The membership order that enrolled the member; <c>null</c> when the member was enrolled by hand.</summary>
    public int? MembershipOrderId { get; private set; }

    /// <summary>
    /// The sports the member books for free in this season (the courts of other sports they buy, see court sales).
    /// Empty: every sport - enrollments of clubs with one sport and every enrollment made before sports existed.
    /// </summary>
    public Sport[] Sports { get; private set; } = [];

    public bool Covers(Sport sport) => Courts.Sports.Covers(Sports, sport);

    public void SetSports(IEnumerable<Sport>? sports) => Sports = Courts.Sports.Normalize(sports);

    /// <summary>Another order holds the enrollment now (the order that made it was revoked, a later one still pays for it).</summary>
    public void SetMembershipOrder(int? membershipOrderId) => MembershipOrderId = membershipOrderId;
}
