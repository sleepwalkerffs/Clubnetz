using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Club;

public record ClubInformationResult
{
    public required TimeOnlyInterval OpeningHours { get; init; }
    public required PrimeTimeSettingsDto PrimeTimeSettings { get; init; }
    public required int? BookingGracePeriodInMinutes { get; init; }

    /// <summary>Where the courts are; members see the weather forecast when both are set.</summary>
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
};

