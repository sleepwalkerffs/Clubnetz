using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Families.Events;
using Fusonic.Extensions.Common.Entities;

namespace Bookennis.Domain.Families;

[Guid("78EAF9AD-66C2-4D65-935B-4668458F7F02")]
public class Family : TenantDomainEntity, IAggregateRoot
{
    private readonly List<FamilyMember> familyMembers = new();
    private readonly List<ParentFamilyMember> parents = new();
    private readonly List<ChildFamilyMember> children = new();

    public IReadOnlyList<FamilyMember> FamilyMembers => familyMembers.AsReadOnly();
    public IReadOnlyList<ParentFamilyMember> Parents => parents.AsReadOnly();
    public IReadOnlyList<ChildFamilyMember> Children => children.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private Family() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public Family(int clubId, List<int> parentMemberIds, List<int> childMemberIds)
    {
        if (parentMemberIds.Count == 0)
            throw new ArgumentException("A family must have at least one parent");

        if (parentMemberIds.Count > 2)
            throw new ArgumentException("A family can only contain two parents");

        ClubId = clubId;
        parents = parentMemberIds.Select(id => new ParentFamilyMember(this, id)).ToList();
        children = childMemberIds.Select(id => new ChildFamilyMember(this, id)).ToList();
        familyMembers.AddRange(parents);
        familyMembers.AddRange(children);
    }

    public void AddParent(int memberId)
    {
        if (Children.Any(i => i.MemberId == memberId))
            throw new ArgumentException("Member is already used as child");

        if (Parents.Any(i => i.MemberId == memberId))
            return;

        if (Parents.Count == 2)
            throw new ArgumentException("Can't set more than two parents");

        parents.Add(new ParentFamilyMember(this, memberId));
    }

    public void AddChildren(List<int> memberIds)
    {
        if (Parents.Any(i => memberIds.Contains(i.MemberId)))
            throw new ArgumentException("One of the provided Members is already used as parent");

        foreach (var memberId in memberIds)
        {
            if (Children.Any(i => i.MemberId == memberId))
                return;

            children.Add(new ChildFamilyMember(this, memberId));
        }
    }

    public void DeleteFamilyMember(int memberId)
    {
        var child = children.SingleOrDefault(i => i.MemberId == memberId);
        if (child is not null)
        {
            children.Remove(child);
            AddDomainEvent(new ChildrenRemovedDomainEvent(Id, [memberId]));
            return;
        }

        var parent = parents.SingleOrDefault(i => i.MemberId == memberId);
        if (parent is not null)
        {
            parents.Remove(parent);
            AddDomainEvent(new ParentsRemovedDomainEvent(Id, [memberId]));
            return;
        }

        throw new EntityNotFoundException();
    }
}