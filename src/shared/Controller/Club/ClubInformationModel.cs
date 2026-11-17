using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Club;
public record ClubInformationModel
{
    public required TimeOnlyInterval OpeningHours { get; init; }
    public required PrimeTimeSettingsDto PrimeTimeSettings { get; init; }
    public required int? BookingGracePeriodInMinutes { get; init; }

    /// <summary>Bookings per week of every member without an own value, and the most a member may get. <c>null</c>: unchanged.</summary>
    public int? BookingsPerWeek { get; init; }
}
