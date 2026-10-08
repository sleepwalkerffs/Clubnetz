namespace Bookennis.Shared.Controller.CourtBlockings;

/// <summary>
/// Create and update model of a court blocking. The blocking lasts from the start date and time until the end date and time
/// (local times in <see cref="TimeZoneInfoId"/>, both times <c>null</c> for all day) and is optionally repeated every n weeks.
/// Texts are validated by the domain, so empty texts result in a localized error instead of a generic bad request.
/// The times are <see cref="TimeSpan"/>s on purpose: the client serializes <see cref="TimeOnly"/> as UTC time of day, but these are local times.
/// </summary>
public record SaveCourtBlockingModel
{
    public string? Title { get; init; }
    public List<int> CourtIds { get; init; } = [];
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public string? TimeZoneInfoId { get; init; }
    public int? RecurrenceIntervalWeeks { get; init; }

    /// <summary>Last day on which a repetition may start.</summary>
    public DateOnly? RecurrenceEndDate { get; init; }

    /// <summary>Confirms that the upcoming bookings in the blocked time are deleted and their players are notified.</summary>
    public bool DeleteConflictingBookings { get; init; }
}
