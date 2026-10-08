using System.Runtime.InteropServices;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Bookings;

[Guid("B8D4F2E5-6A93-4C0B-9F72-3E8A1D5B0C46")]
public class RecurringBookingSeriesPlayer : DomainEntity
{
#pragma warning disable CS8618
    private RecurringBookingSeriesPlayer() { }
#pragma warning restore CS8618

    public RecurringBookingSeriesPlayer(RecurringBookingSeries series, int memberId)
    {
        Series = series;
        RecurringBookingSeriesId = series.Id;
        MemberId = memberId;
    }

    public RecurringBookingSeries Series { get; private set; }
    public int RecurringBookingSeriesId { get; private set; }
    public int MemberId { get; private set; }
}
