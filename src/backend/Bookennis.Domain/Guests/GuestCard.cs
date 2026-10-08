using System.Runtime.InteropServices;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Guests;

[Guid("714C2F50-088C-4D20-A90F-DBC8A148C2FC")]
public class GuestCard : TenantDomainEntity
{
    public GuestCard(int clubId, Guid code, int purchasedBookings, int guestMemberId)
    {
        ClubId = clubId;
        Code = code;
        PurchasedBookings = purchasedBookings;
        GuestMemberId = guestMemberId;
    }

    public Guid Code { get; private set; }
    public int PurchasedBookings { get; private set; }
    public int GuestMemberId { get; private set; }
}
