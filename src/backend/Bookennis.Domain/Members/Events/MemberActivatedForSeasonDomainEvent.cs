using Bookennis.Domain.Base;

namespace Bookennis.Domain.Members.Events;

public record MemberActivatedForSeasonDomainEvent(int MemberId, int SeasonId) : IDomainEvent;
