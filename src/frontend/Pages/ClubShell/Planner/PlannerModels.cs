using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Pages.ClubShell.Planner;

/// <summary>
/// A region the user selected in the planner (sent by <c>planner-interop.js</c>):
/// the courts at the indexes <see cref="CourtStart"/>..<see cref="CourtEnd"/> (inclusive, in the order of the rows)
/// from <see cref="From"/> to <see cref="To"/> (Unix milliseconds) within one day.
/// </summary>
public record PlannerRegion(int CourtStart, int CourtEnd, long From, long To);

/// <summary>A booking dropped at a new court or time (Unix milliseconds), sent by <c>planner-interop.js</c>.</summary>
public record PlannerMove(int BookingId, int CourtId, long From, long To);

/// <summary>One booking to create: a court and a time span of a selected region.</summary>
public record PlannerSlot(int CourtId, string CourtName, DateTimeOffset From, DateTimeOffset To);

public static class PlannerHelpers
{
    /// <summary>The roles that may change any booking, also past ones (same as <c>BookingDetail</c>).</summary>
    public static readonly MemberRole[] ElevatedRoles =
    [
        MemberRole.Maintainer,
        MemberRole.Admin,
        MemberRole.SportsDirector,
        MemberRole.YouthSportsDirector,
        MemberRole.Trainer,
    ];

    public static readonly int[] DayOptions = [3, 7, 14];

    /// <summary>Width of a 30 minute slot in pixels for the zoom levels.</summary>
    public static readonly int[] SlotWidths = [16, 24, 36];

    public const int DefaultDays = 7;
    public const int DefaultSlotWidth = 24;
    public const int SlotMinutes = 30;

    public static bool HasElevatedRole(IEnumerable<MemberRole> roles) => roles.Any(ElevatedRoles.Contains);

    /// <summary>
    /// Play modes the user may book: the allowed roles of the mode, admins (and support mode) all of them - except the old modes
    /// only for guests, which nobody books any more since the guest system was removed.
    /// </summary>
    public static bool MayUsePlayMode(PlayModeDto playMode, MemberRole[] roles, bool isSupportMode)
        => !IsGuestOnly(playMode) && (isSupportMode || roles.Contains(MemberRole.Admin) || playMode.AllowedRoles.Any(roles.Contains));

    private static bool IsGuestOnly(PlayModeDto playMode)
        => playMode.AllowedRoles.Count > 0 && playMode.AllowedRoles.All(r => r == MemberRole.Guest);

    public static DateTimeOffset FromUnixMilliseconds(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime();

    /// <summary>
    /// The time window of a daily interval (opening hours, prime time) on a date.
    /// The intervals are wall-clock times of the club, so they are placed on the date as they are: a day after a daylight saving
    /// switch keeps its times. <c>To</c> = 00:00 (or any time not after <c>From</c>) means the next day, e.g. midnight
    /// (<see cref="TimeOnlyInterval.EndOfDay"/>).
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset To) DailyWindow(DateOnly date, TimeOnlyInterval localInterval)
    {
        var endDate = localInterval.To > localInterval.From ? date : date.AddDays(1);
        return (new DateTimeOffset(date.ToDateTime(localInterval.From)), new DateTimeOffset(endDate.ToDateTime(localInterval.To)));
    }
}
