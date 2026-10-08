using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.CourtBlockings;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtBlockings;

public static class CourtBlockingQueryExtensions
{
    public static CourtBlocking.BlockingData ToBlockingData(this SaveCourtBlockingModel model) => new(
        model.Title,
        model.CourtIds,
        model.StartDate,
        model.EndDate,
        ToTimeOnly(model.StartTime),
        ToTimeOnly(model.EndTime),
        model.TimeZoneInfoId,
        model.RecurrenceIntervalWeeks,
        model.RecurrenceEndDate);

    private static TimeOnly? ToTimeOnly(TimeSpan? time)
    {
        if (time is not { } value)
            return null;

        if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1))
            throw new PreconditionException(CourtBlocking.ErrorCode.CourtBlockingInvalidTimeRange, "The time is not a time of day.");

        return TimeOnly.FromTimeSpan(value);
    }

    public static Task<CourtBlocking> GetBlockingWithDetails(this IQueryable<CourtBlocking> blockings, int clubId, int courtBlockingId, CancellationToken cancellationToken)
        => blockings
            .Include(b => b.Courts)
            .Include(b => b.Occurrences)
            .AsSplitQuery()
            .SingleRequiredAsync(b => b.Id == courtBlockingId && b.ClubId == clubId, cancellationToken);

    /// <summary>Titles of the blockings that block the court at some point in the interval.</summary>
    public static Task<List<string>> GetCourtBlockingTitles(this AppDbContext context, int courtId, DateTimeOffsetInterval interval, CancellationToken cancellationToken)
    {
        var from = interval.From;
        var to = interval.To;
        return context.CourtBlockings
            .Where(b => b.Courts.Any(c => c.CourtId == courtId) && b.Occurrences.Any(o => o.Interval.From < to && o.Interval.To > from))
            .OrderBy(b => b.Title)
            .Select(b => b.Title)
            .ToListAsync(cancellationToken);
    }

    public static Task<bool> IsCourtBlocked(this AppDbContext context, int courtId, DateTimeOffsetInterval interval, CancellationToken cancellationToken)
    {
        var from = interval.From;
        var to = interval.To;
        return context.CourtBlockings
            .AnyAsync(b => b.Courts.Any(c => c.CourtId == courtId) && b.Occurrences.Any(o => o.Interval.From < to && o.Interval.To > from), cancellationToken);
    }

    public static async Task EnsureCourtsBelongToClub(this AppDbContext context, int clubId, IReadOnlyCollection<int> courtIds, CancellationToken cancellationToken)
    {
        var distinctCourtIds = courtIds.Distinct().ToList();
        var existingCourts = await context.Courts.CountAsync(c => c.ClubId == clubId && distinctCourtIds.Contains(c.Id), cancellationToken);
        if (existingCourts != distinctCourtIds.Count)
            throw new EntityNotFoundException(typeof(Court));
    }

    /// <summary>
    /// The bookings on the blocked courts that intersect a blocked time window and have not started yet.
    /// Bookings that already started or are over stay untouched, they are part of the statistics.
    /// </summary>
    public static async Task<List<Booking>> GetConflictingBookings(this AppDbContext context, CourtBlocking blocking, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (blocking.Occurrences.Count == 0)
            return [];

        var clubId = blocking.ClubId;
        var courtIds = blocking.Courts.Select(c => c.CourtId).ToList();
        var from = blocking.Occurrences.Min(o => o.Interval.From);
        var to = blocking.Occurrences.Max(o => o.Interval.To);

        // The occurrences of a recurring blocking are matched in memory, the query only narrows it down to the whole period
        var candidates = await context.Bookings
            .Where(b => b.ClubId == clubId && courtIds.Contains(b.CourtId) && b.Interval.From > now && b.Interval.From < to && b.Interval.To > from)
            .OrderBy(b => b.Interval.From)
            .ToListAsync(cancellationToken);

        return candidates.Where(b => blocking.Blocks(b.CourtId, b.Interval)).ToList();
    }

    /// <summary>Deleting bookings has to be confirmed by the user, who saw the affected bookings before.</summary>
    public static async Task EnsureConflictsAreConfirmed(this AppDbContext context, CourtBlocking blocking, bool deleteConflictingBookings, CancellationToken cancellationToken)
    {
        if (deleteConflictingBookings)
            return;

        var conflicts = await context.GetConflictingBookings(blocking, DateTimeOffset.UtcNow, cancellationToken);
        if (conflicts.Count > 0)
        {
            throw new PreconditionException(
                CourtBlocking.ErrorCode.CourtBlockingHasConflictingBookings,
                [conflicts.Count.ToString()],
                "There are bookings in the blocked time.");
        }
    }
}
