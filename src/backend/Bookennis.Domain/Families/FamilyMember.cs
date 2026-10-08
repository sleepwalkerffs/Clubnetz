using Bookennis.Domain.Base;

namespace Bookennis.Domain.Families;

public abstract class FamilyMember : DomainEntity
{
#pragma warning disable CS8618
    protected FamilyMember() { }
#pragma warning restore CS8618
    protected FamilyMember(Family family, int memberId)
    {
        MemberId = memberId;
        Family = family;
    }
    public int MemberId { get; private set; }
    public Family Family { get; private set; }
    public int FamilyId { get; set; }
}

public class ChildFamilyMember : FamilyMember
{
#pragma warning disable CS8618
    private ChildFamilyMember() { }
#pragma warning restore CS8618

    public ChildFamilyMember(Family childFamily, int memberId) : base(childFamily, memberId)
        => ChildFamily = childFamily;
    public Family ChildFamily { get; private set; }
    public int ChildFamilyId { get; private set; }
}

public class ParentFamilyMember : FamilyMember
{
#pragma warning disable CS8618
    private ParentFamilyMember() { }
#pragma warning restore CS8618

    public ParentFamilyMember(Family family, int memberId) : base(family, memberId)
        => ParentFamily = family;
    public Family ParentFamily { get; private set; }
    public int ParentFamilyId { get; private set; }
}