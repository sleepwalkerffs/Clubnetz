namespace Bookennis.Api.Config;

/// <summary>
/// The weather forecast of MET Norway (Locationforecast 2.0). Free, also for commercial use, under CC BY 4.0: every page that shows
/// the data credits MET Norway. Their terms ask for a <see cref="UserAgent"/> that names the app and a way to contact it, at most
/// 4 decimals for the coordinates and no new request before the <c>Expires</c> header of the last answer.
/// </summary>
public class WeatherSettings
{
    /// <summary>Switch the fetching off, e.g. on a review environment. Members then see no weather.</summary>
    public bool IsEnabled { get; set; } = true;

    public string ApiUrl { get; set; } = "https://api.met.no/weatherapi/locationforecast/2.0/complete";

    public string UserAgent { get; set; } = "Clubnetz/1.0 (+https://clubnetz.app)";
}
