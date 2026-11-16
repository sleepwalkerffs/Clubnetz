using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record UpdateAdminClubRequest
{
    public required string Name { get; init; }
    public required TimeOnlyInterval OpeningHours { get; init; }
    public required PrimeTimeSettingsDto PrimeTimeSettings { get; init; }
    public int? BookingGracePeriodInMinutes { get; init; }
    public required bool IsAtpClub { get; init; }
    // Not sent by older clients (e.g. a cached app right after an update): null keeps what the club has
    public string? TimeZoneId { get; init; }
    public Country? Country { get; init; }
    public string? Currency { get; init; }
}
