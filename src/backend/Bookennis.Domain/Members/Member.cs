using System.Runtime.InteropServices;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Members;

[Guid("4754467B-0A86-4A5D-B9D2-B85CCA87DAF8")]
public abstract class Member : TenantDomainEntity, IAggregateRoot
{
    public Member(int userId, int clubId, MemberRole[] userRoles, MemberType memberType)
    {
        UserId = userId;
        ClubId = clubId;
        UserRoles = userRoles;
        MemberType = memberType;
    }

    public int UserId { get; private set; }
    public MemberRole[] UserRoles { get; private set; }
    public MemberType MemberType { get; private set; }
    public bool IsAllowedToBook { get; protected set; }
    public int? BookingsPerWeek { get; private set; }

    public void Update(int bookingsPerWeek)
    {
        BookingsPerWeek = bookingsPerWeek;
    }

    public void UpdateRoles(MemberRole[] roles) => UserRoles = roles;

    public abstract bool CanParticipate { get; }
}
