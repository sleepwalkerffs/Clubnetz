using Bookennis.Domain.Base;

namespace Bookennis.Domain.Courts;

/// <summary>A court that is blocked by a <see cref="CourtBlocking"/>.</summary>
public class CourtBlockingCourt : DomainEntity
{
#pragma warning disable CS8618
    private CourtBlockingCourt() { }
#pragma warning restore CS8618

    internal CourtBlockingCourt(CourtBlocking blocking, int courtId)
    {
        Blocking = blocking;
        CourtBlockingId = blocking.Id;
        CourtId = courtId;
    }

    public CourtBlocking Blocking { get; private set; }
    public int CourtBlockingId { get; private set; }
    public int CourtId { get; private set; }
}
