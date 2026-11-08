namespace Bookennis.Shared.Controller.Weather;

/// <summary>Where the courts of the club are, in decimal degrees. Both <c>null</c> switches the weather forecast off.</summary>
public record ClubLocationModel
{
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}

public record GetWeatherForecastResult
{
    /// <summary><c>false</c> when the club has no location or the forecast could not be fetched for a while: show no weather.</summary>
    public required bool IsAvailable { get; init; }

    public required List<WeatherPeriodDto> Periods { get; init; }
}

/// <summary>
/// The weather of one period (an hour, further ahead 6 hours). <see cref="Symbol"/> is the symbol code of MET Norway, e.g.
/// <c>partlycloudy_day</c>. <see cref="PrecipitationProbability"/> is in percent.
/// </summary>
public record WeatherPeriodDto
{
    public required DateTimeOffset From { get; init; }
    public required int Hours { get; init; }
    public required string Symbol { get; init; }
    public decimal? TemperatureCelsius { get; init; }
    public decimal? PrecipitationMm { get; init; }
    public decimal? PrecipitationProbability { get; init; }
    public decimal? WindSpeedMs { get; init; }

    public DateTimeOffset To => From.AddHours(Hours);

    public bool Contains(DateTimeOffset time) => From <= time && time < To;
}
