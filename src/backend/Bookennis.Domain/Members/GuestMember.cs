using System.Runtime.InteropServices;

namespace Bookennis.Domain.Members;

[Guid("B92CC152-0298-446E-B9C8-6091B434A4F0")]
public class GuestMember(int userId, int clubId)
    : Member(userId, clubId, [MemberRole.Guest], MemberType.GuestMember)
{
    public override bool CanParticipate => IsAllowedToBook;

    public void AllowBooking() => IsAllowedToBook = true;
}
