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
}
