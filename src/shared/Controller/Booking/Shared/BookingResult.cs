using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Booking.Shared;

public record BookingResult(int BookingEntryId, List<PlayerResult> Players, int CourtId, DateTimeOffsetInterval Interval, PlayModeDto PlayMode, string? Comment, int? RecurringBookingSeriesId = null, bool IsExcludedFromSeries = false);