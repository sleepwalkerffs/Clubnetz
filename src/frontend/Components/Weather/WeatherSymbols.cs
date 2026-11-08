using System.Globalization;
using Bookennis.Shared.Controller.Weather;

namespace Bookennis.Client.Components.Weather;

/// <summary>
/// The symbol codes of MET Norway (e.g. <c>partlycloudy_day</c>, <c>lightrainshowersandthunder_night</c>) as an emoji and a text key of
/// <c>WeatherLocale</c>. Emoji need no image licence and look the same in light and dark mode.
/// </summary>
public static class WeatherSymbols
{
    public static string Emoji(string symbol)
    {
        var (kind, isNight) = Kind(symbol);
        return kind switch
        {
            "clearsky" => isNight ? "🌙" : "☀️",
            "fair" => isNight ? "🌙" : "🌤️",
            "partlycloudy" => isNight ? "☁️" : "⛅",
            "cloudy" => "☁️",
            "fog" => "🌫️",
            "rainshowers" => isNight ? "🌧️" : "🌦️",
            "rain" => "🌧️",
            "thunder" => "⛈️",
            "sleet" => "🌨️",
            "snow" => "❄️",
            _ => "🌡️",
        };
    }

    /// <summary>The key of the description in <c>WeatherLocale</c>, e.g. <c>Weather_rain</c>.</summary>
    public static string TextKey(string symbol) => $"Weather_{Kind(symbol).Kind}";

    public static string Temperature(decimal? celsius)
        => celsius is { } value ? $"{Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.CurrentCulture)}°" : "";

    /// <summary>Wind in km/h, rounded (MET Norway gives m/s).</summary>
    public static string WindKmh(decimal metersPerSecond) => Math.Round(metersPerSecond * 3.6m).ToString("0", CultureInfo.CurrentCulture);

    /// <summary>Rain is worth a warning colour from this probability on.</summary>
    public static bool IsRainLikely(WeatherPeriodDto period) => period.PrecipitationProbability is >= 50 || period.PrecipitationProbability is null && period.PrecipitationMm is >= 0.5m;

    private static (string Kind, bool IsNight) Kind(string symbol)
    {
        var parts = symbol.Split('_');
        var code = parts[0];
        var isNight = parts.Length > 1 && parts[1] == "night";

        if (code.Contains("thunder", StringComparison.Ordinal))
            return ("thunder", isNight);
        if (code.Contains("snow", StringComparison.Ordinal))
            return ("snow", isNight);
        if (code.Contains("sleet", StringComparison.Ordinal))
            return ("sleet", isNight);
        if (code.Contains("rainshowers", StringComparison.Ordinal))
            return ("rainshowers", isNight);
        if (code.Contains("rain", StringComparison.Ordinal))
            return ("rain", isNight);

        return code is "clearsky" or "fair" or "partlycloudy" or "cloudy" or "fog" ? (code, isNight) : ("unknown", isNight);
    }
}
