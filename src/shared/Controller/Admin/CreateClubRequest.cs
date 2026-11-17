using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Admin;

public record CreateClubRequest
{
    public required string Name { get; init; }
    public required TimeOnlyInterval OpeningHours { get; init; }
    public required TimeOnlyInterval PrimeTimeHours { get; init; }
    public int? BookingGracePeriodInMinutes { get; init; }
    public string TimeZoneId { get; init; } = "Europe/Vienna";
    public Bookennis.Shared.Controller.Shared.Country Country { get; init; }
    public string Currency { get; init; } = "eur";
}
