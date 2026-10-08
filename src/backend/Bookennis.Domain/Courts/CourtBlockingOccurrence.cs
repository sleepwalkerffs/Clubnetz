using Bookennis.Domain.Base;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Courts;

/// <summary>A time window (UTC) in which the courts of a <see cref="CourtBlocking"/> can't be booked.</summary>
public class CourtBlockingOccurrence : DomainEntity
{
#pragma warning disable CS8618
    private CourtBlockingOccurrence() { }
#pragma warning restore CS8618

    internal CourtBlockingOccurrence(CourtBlocking blocking, DateTimeOffsetInterval interval)
    {
        Blocking = blocking;
        CourtBlockingId = blocking.Id;
        Interval = interval;
    }

    public CourtBlocking Blocking { get; private set; }
    public int CourtBlockingId { get; private set; }
    public DateTimeOffsetInterval Interval { get; private set; }
}
