using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Courts.Events;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Courts;

/// <summary>
/// Blocks one or more courts for a time window (e.g. a tournament, maintenance or bad weather), optionally repeating every n weeks.
/// The blocked time windows are stored as <see cref="Occurrences"/>, so bookings can be checked against them in the database.
/// </summary>
[Guid("6B1E5B0C-6E0A-4A64-9E55-0B7B3A2B8F41")]
public class CourtBlocking : TenantDomainEntity, IAggregateRoot
{
    public const int MaxTitleLength = 100;

    /// <summary>Longest time window of a single occurrence.</summary>
    public const int MaxDurationInDays = 366;

    public const int MaxRecurrenceIntervalWeeks = 52;

    /// <summary>A weekly blocking can cover two years.</summary>
    public const int MaxOccurrences = 105;

    public enum ErrorCode
    {
        CourtBlockingTitleRequired = 0,
        CourtBlockingTitleTooLong = 1,
        CourtBlockingCourtRequired = 2,
        CourtBlockingInvalidDateRange = 3,
        CourtBlockingInvalidTimeRange = 4,
        CourtBlockingInvalidRecurrence = 5,
        CourtBlockingTooManyOccurrences = 6,
        CourtBlockingInvalidTimeZone = 7,

        /// <summary>Bookings in the blocked time are only deleted after the user confirmed it.</summary>
        CourtBlockingHasConflictingBookings = 8,
    }

    /// <summary>
    /// <c>StartTime</c>/<c>EndTime</c>: local times in <c>TimeZoneInfoId</c>, both <c>null</c> for all day.
    /// The blocking lasts from the start date and time until the end date and time.
    /// <c>RecurrenceIntervalWeeks</c>: <c>null</c> for a single blocking, otherwise it is repeated until <c>RecurrenceEndDate</c> (last possible start date).
    /// </summary>
    public record BlockingData(
        string? Title,
        IReadOnlyCollection<int> CourtIds,
        DateOnly StartDate,
        DateOnly EndDate,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        string? TimeZoneInfoId,
        int? RecurrenceIntervalWeeks,
        DateOnly? RecurrenceEndDate);

    private readonly List<CourtBlockingCourt> courts = new();
    private readonly List<CourtBlockingOccurrence> occurrences = new();

#pragma warning disable CS8618
    private CourtBlocking() { }
#pragma warning restore CS8618

    public CourtBlocking(int clubId, BlockingData data)
    {
        ClubId = clubId;
        Update(data);
    }

    /// <summary>The reason shown in the booking grid, e.g. "Club championship".</summary>
    public string Title { get; private set; } = "";

    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    /// <summary>Local start time; <c>null</c> for all day.</summary>
    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }
    public string TimeZoneInfoId { get; private set; } = "";
    public int? RecurrenceIntervalWeeks { get; private set; }
    public DateOnly? RecurrenceEndDate { get; private set; }

    public IReadOnlyList<CourtBlockingCourt> Courts => courts.AsReadOnly();
    public IReadOnlyList<CourtBlockingOccurrence> Occurrences => occurrences.AsReadOnly();

    public bool IsAllDay => StartTime is null;
    public bool IsRecurring => RecurrenceIntervalWeeks is not null;

    /// <summary>
    /// Replaces all data. The occurrences are only regenerated when the blocked time changed.
    /// Raises <see cref="CourtBlockedDomainEvent"/> when courts or times were added, so bookings in the blocked time can be removed.
    /// </summary>
    public void Update(BlockingData data)
    {
        var title = data.Title?.Trim() ?? "";
        if (title.Length == 0)
            throw new PreconditionException(ErrorCode.CourtBlockingTitleRequired, "A title is required.");

        if (title.Length > MaxTitleLength)
            throw new PreconditionException(ErrorCode.CourtBlockingTitleTooLong, [MaxTitleLength.ToString()], "The title is too long.");

        var courtIds = data.CourtIds.Distinct().ToList();
        if (courtIds.Count == 0)
            throw new PreconditionException(ErrorCode.CourtBlockingCourtRequired, "At least one court is required.");

        if (data.EndDate < data.StartDate || data.EndDate.DayNumber - data.StartDate.DayNumber >= MaxDurationInDays)
            throw new PreconditionException(ErrorCode.CourtBlockingInvalidDateRange, "The end date must not be before the start date.");

        if (data.StartTime.HasValue != data.EndTime.HasValue || (data.StartDate == data.EndDate && data.EndTime <= data.StartTime))
            throw new PreconditionException(ErrorCode.CourtBlockingInvalidTimeRange, "The end time must be after the start time.");

        var timeZone = FindTimeZone(data.TimeZoneInfoId);
        var intervals = CalculateIntervals(data, timeZone);

        var blockedTimeChanged = occurrences.Count != intervals.Count
            || occurrences.Zip(intervals).Any(x => x.First.Interval.From != x.Second.From || x.First.Interval.To != x.Second.To);
        var courtsAdded = courtIds.Except(courts.Select(c => c.CourtId)).Any();

        Title = title;
        StartDate = data.StartDate;
        EndDate = data.EndDate;
        StartTime = data.StartTime;
        EndTime = data.EndTime;
        TimeZoneInfoId = timeZone.Id;
        RecurrenceIntervalWeeks = data.RecurrenceIntervalWeeks;
        RecurrenceEndDate = data.RecurrenceIntervalWeeks is null ? null : data.RecurrenceEndDate;

        courts.RemoveAll(c => !courtIds.Contains(c.CourtId));
        foreach (var courtId in courtIds.Where(id => courts.All(c => c.CourtId != id)))
            courts.Add(new CourtBlockingCourt(this, courtId));

        if (blockedTimeChanged)
        {
            occurrences.Clear();
            occurrences.AddRange(intervals.Select(interval => new CourtBlockingOccurrence(this, interval)));
        }

        if (blockedTimeChanged || courtsAdded)
            AddDomainEvent(new CourtBlockedDomainEvent());
    }

    public bool Blocks(int courtId, DateTimeOffsetInterval interval)
        => courts.Any(c => c.CourtId == courtId) && occurrences.Any(o => o.Interval.Intersects(interval));

    private static List<DateTimeOffsetInterval> CalculateIntervals(BlockingData data, TimeZoneInfo timeZone)
    {
        var durationInDays = data.EndDate.DayNumber - data.StartDate.DayNumber;
        var startDates = new List<DateOnly> { data.StartDate };

        if (data.RecurrenceIntervalWeeks is { } weeks)
        {
            // Occurrences of a recurring blocking must not overlap each other
            if (weeks < 1 || weeks > MaxRecurrenceIntervalWeeks || data.RecurrenceEndDate is not { } recurrenceEndDate || recurrenceEndDate < data.StartDate || durationInDays >= weeks * 7)
                throw new PreconditionException(ErrorCode.CourtBlockingInvalidRecurrence, "The recurrence is invalid.");

            for (var date = data.StartDate.AddDays(weeks * 7); date <= recurrenceEndDate; date = date.AddDays(weeks * 7))
            {
                if (startDates.Count >= MaxOccurrences)
                    throw new PreconditionException(ErrorCode.CourtBlockingTooManyOccurrences, [MaxOccurrences.ToString()], "The blocking repeats too often.");

                startDates.Add(date);
            }
        }

        return startDates.ConvertAll(startDate =>
        {
            var endDate = startDate.AddDays(durationInDays);
            var from = ToUtc(startDate.ToDateTime(data.StartTime ?? TimeOnly.MinValue), timeZone);
            var to = data.EndTime is { } endTime
                ? ToUtc(endDate.ToDateTime(endTime), timeZone)
                : ToUtc(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

            return new DateTimeOffsetInterval(from, to);
        });
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo timeZone) => new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();

    private static TimeZoneInfo FindTimeZone(string? timeZoneInfoId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneInfoId ?? "");
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new PreconditionException(ErrorCode.CourtBlockingInvalidTimeZone, "The time zone is unknown.");
        }
    }
}
