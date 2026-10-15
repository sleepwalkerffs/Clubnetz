using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Clubs;

public class Season : TenantDomainEntity
{
    public enum ErrorCode
    {
        WrappedSeasonNotStarted = 0,
    }

#pragma warning disable 8618
    private Season() { }
#pragma warning restore 8618

    public Season(int clubId, DateOnlyInterval period)
    {
        ClubId = clubId;
        Period = period;
    }

    public DateOnlyInterval Period { get; private set; }

    /// <summary>
    /// When the club admins released the season's Wrapped (the season review of every member). <c>null</c> = not released,
    /// only the admins can preview it. The content is not stored, it is calculated from the bookings when a member opens it.
    /// </summary>
    public DateTimeOffset? WrappedPublishedAt { get; private set; }

    public bool IsWrappedPublished => WrappedPublishedAt is not null;

    public void Update(DateOnlyInterval period)
    {
        Period = period;
    }

    public bool IsActiveOn(DateOnly date) => Period.From <= date && date <= Period.To;

    public bool HasStartedOn(DateOnly date) => Period.From <= date;

    /// <summary>Releases the Wrapped to the members. A season that has not started has nothing to look back on. Repeating keeps the first date.</summary>
    public void PublishWrapped(DateTimeOffset now, DateOnly today)
    {
        if (!HasStartedOn(today))
            throw new PreconditionException(ErrorCode.WrappedSeasonNotStarted, "The Wrapped of a season can only be released once the season has started.");

        WrappedPublishedAt ??= now;
    }

    public void UnpublishWrapped() => WrappedPublishedAt = null;
}
