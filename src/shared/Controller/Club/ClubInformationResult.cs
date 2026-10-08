using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Club;

public record ClubInformationResult
{
    public required TimeOnlyInterval OpeningHours { get; init; }
    public required PrimeTimeSettingsDto PrimeTimeSettings { get; init; }
    public required int? BookingGracePeriodInMinutes { get; init; }
};

