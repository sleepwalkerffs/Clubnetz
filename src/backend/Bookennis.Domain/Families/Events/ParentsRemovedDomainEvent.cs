using Bookennis.Domain.Base;

namespace Bookennis.Domain.Families.Events;

public record ParentsRemovedDomainEvent(int FamilyId, List<int> ParentMemberIds) : IDomainEvent;