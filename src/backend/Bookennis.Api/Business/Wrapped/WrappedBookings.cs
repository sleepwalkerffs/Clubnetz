using Bookennis.Api.Business.Statistics;
using Bookennis.Api.Data;
using Bookennis.Global.Intervals;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Wrapped;

/// <summary>A booking of the club as the Wrapped sees it: in the local time of the booking, with its players.</summary>
internal sealed record WrappedBooking(int Id, DateTime From, double DurationHours, int CourtId, int? RecurringBookingSeriesId, IReadOnlyList<int> PlayerIds)
{
    public DateOnly Date => DateOnly.FromDateTime(From);
}

internal static class WrappedBookings
{
    /// <summary>
    /// The bookings of the club in the period that count for statistics and badges (play modes that charge the booking subscription),
    /// by their local date. Same rule as the statistics, so the Wrapped never tells a different number.
    /// </summary>
    public static async Task<List<WrappedBooking>> Load(AppDbContext context, int clubId, DateOnlyInterval period, CancellationToken cancellationToken)
    {
        // A day of margin on both sides, the local date decides below
        var from = new DateTimeOffset(period.From.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(period.To.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var chargingPlayModeIds = await context.PlayModes
            .Where(pm => pm.ClubId == clubId && pm.IsChargingBookingSubscription)
            .Select(pm => pm.Id)
            .ToListAsync(cancellationToken);

        var rows = await context.Bookings
            .Where(b => b.ClubId == clubId && chargingPlayModeIds.Contains(b.PlayModeId) && b.Interval.From >= from && b.Interval.From < to)
            .Select(b => new { b.Id, b.Interval.From, b.Interval.To, b.TimeZoneInfoId, b.CourtId, b.RecurringBookingSeriesId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var bookingIds = rows.Select(r => r.Id).ToList();
        var players = (await context.BookingPlayers
                .Where(bp => bookingIds.Contains(bp.BookingEntryId))
                .Select(bp => new { bp.BookingEntryId, bp.MemberId })
                .ToListAsync(cancellationToken))
            .ToLookup(bp => bp.BookingEntryId, bp => bp.MemberId);

        var localTime = new LocalTimeConverter();
        return rows
            .Select(r => new WrappedBooking(
                r.Id,
                localTime.ToLocal(r.From, r.TimeZoneInfoId),
                (r.To - r.From).TotalHours,
                r.CourtId,
                r.RecurringBookingSeriesId,
                players[r.Id].Distinct().ToList()))
            .Where(b => period.From <= b.Date && b.Date <= period.To)
            .ToList();
    }
}
