using Bookennis.Domain.Base;
using Bookennis.Domain.Members.Events;

namespace Bookennis.Domain.Members;

public class MemberSeason : DomainEntity
{
    private MemberSeason() { }

    public MemberSeason(int memberId, int seasonId)
    {
        MemberId = memberId;
        SeasonId = seasonId;
        AddDomainEvent(new MemberActivatedForSeasonDomainEvent(memberId, seasonId));
    }

    public int MemberId { get; private set; }
    public int SeasonId { get; private set; }
    public bool LeaderboardOptOut { get; set; }
}
