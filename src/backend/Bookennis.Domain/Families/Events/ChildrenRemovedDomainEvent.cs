using Bookennis.Domain.Base;

namespace Bookennis.Domain.Families.Events;

public record ChildrenRemovedDomainEvent(int FamilyId, List<int> ChildMemberIds) : IDomainEvent;