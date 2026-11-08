namespace Bookennis.Domain.Weather;

/// <summary>
/// The rules of the weather forecast that are broken by invalid input. The names are the error codes the client localizes
/// (<c>BackendErrorCodeLocale</c>), so never rename or renumber them.
/// </summary>
public enum WeatherErrorCode
{
    /// <summary>The latitude is not between -90 and 90 or the longitude not between -180 and 180, or only one of them is set.</summary>
    ClubLocationInvalid = 0,
}
