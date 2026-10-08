using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Shared.Controller.Booking;

public record RecurringBookingSeriesResult
{
    public required int Id { get; init; }
    public required int CourtId { get; init; }
    public required int PlayModeId { get; init; }
    public required DayOfWeek DayOfWeek { get; init; }
    public required TimeOnly StartTime { get; init; }
    public required TimeOnly EndTime { get; init; }
    public required int RecurrenceIntervalWeeks { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly? EndDate { get; init; }
    public required string? Comment { get; init; }
    public required List<PlayerResult> Players { get; init; }
}
