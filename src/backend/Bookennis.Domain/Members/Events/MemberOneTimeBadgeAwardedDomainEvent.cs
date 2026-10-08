using Bookennis.Domain.Base;

namespace Bookennis.Domain.Members.Events;

public record MemberOneTimeBadgeAwardedDomainEvent(int MemberId, int OneTimeBadgeId) : IDomainEvent;
