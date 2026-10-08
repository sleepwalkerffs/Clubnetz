using Bookennis.Domain.Base;

namespace Bookennis.Domain.Members.Events;

public record MemberBadgeAwardedDomainEvent(int MemberId, int BadgeTierId, int SeasonId) : IDomainEvent;
