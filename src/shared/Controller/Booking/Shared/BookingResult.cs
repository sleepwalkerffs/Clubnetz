using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Booking.Shared;

/// <summary>
/// A booking of the calendar. <see cref="IsAnonymized"/>: the caller is not an active member of the club (not enrolled in the
/// active season, no staff role) and plays in no part of this booking: it only shows that the court is booked. <see cref="Players"/> is
/// empty, <see cref="Comment"/> and <see cref="RecurringBookingSeriesId"/> are <c>null</c> and <see cref="PlayMode"/> is a neutral
/// placeholder (id 0, empty name, gray).
/// </summary>
public record BookingResult(int BookingEntryId, List<PlayerResult> Players, int CourtId, DateTimeOffsetInterval Interval, PlayModeDto PlayMode, string? Comment, int? RecurringBookingSeriesId = null, bool IsExcludedFromSeries = false, bool IsAnonymized = false, bool IsBought = false);