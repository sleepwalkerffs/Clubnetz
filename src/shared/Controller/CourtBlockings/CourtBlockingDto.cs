using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.CourtBlockings;

public record GetCourtBlockingsResult
{
    public required List<CourtBlockingDto> Blockings { get; init; }
}

/// <summary>A court blocking as it was entered, for the management page.</summary>
public record CourtBlockingDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required List<int> CourtIds { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }

    /// <summary>Local start time of day; <c>null</c> for all day. See <see cref="SaveCourtBlockingModel"/> for why it is no <see cref="TimeOnly"/>.</summary>
    public required TimeSpan? StartTime { get; init; }

    public required TimeSpan? EndTime { get; init; }
    public required int? RecurrenceIntervalWeeks { get; init; }
    public required DateOnly? RecurrenceEndDate { get; init; }
    public required int OccurrenceCount { get; init; }

    /// <summary>Start of the running or next blocked time window; <c>null</c> when the blocking is over.</summary>
    public required DateTimeOffset? NextOccurrence { get; init; }
}

public record GetCourtBlockingOccurrencesResult
{
    public required List<CourtBlockingOccurrenceDto> Occurrences { get; init; }
}

/// <summary>A blocked time window for the booking grid.</summary>
public record CourtBlockingOccurrenceDto
{
    public required int CourtBlockingId { get; init; }
    public required string Title { get; init; }
    public required List<int> CourtIds { get; init; }
    public required DateTimeOffsetInterval Interval { get; init; }
    public required bool IsRecurring { get; init; }
}

public record GetCourtBlockingConflictsResult
{
    public required List<CourtBlockingConflictDto> Bookings { get; init; }
}

/// <summary>An upcoming booking that would be deleted by a court blocking.</summary>
public record CourtBlockingConflictDto
{
    public required int BookingId { get; init; }
    public required string CourtName { get; init; }
    public required DateTimeOffsetInterval Interval { get; init; }
    public required List<string> Players { get; init; }
}
