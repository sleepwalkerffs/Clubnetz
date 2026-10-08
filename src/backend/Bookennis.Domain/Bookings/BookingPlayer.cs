using System.Runtime.InteropServices;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Bookings;

[Guid("18AEF882-58A2-4E0E-B707-FF94BDED6DE1")]
public class BookingPlayer : DomainEntity
{
#pragma warning disable CS8618
    private BookingPlayer() { }
#pragma warning restore CS8618

    public BookingPlayer(Booking booking, int memberId)
    {
        Booking = booking;
        BookingEntryId = booking.Id;
        MemberId = memberId;
    }

    public Booking Booking { get; private set; }
    public int BookingEntryId { get; private set; }
    public int MemberId { get; private set; }
}
