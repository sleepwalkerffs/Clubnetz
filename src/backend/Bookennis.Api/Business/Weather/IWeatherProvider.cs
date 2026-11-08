using Bookennis.Domain.Weather;

namespace Bookennis.Api.Business.Weather;

/// <summary>Where the forecast comes from (<see cref="MetNorwayWeatherProvider"/>). A substitute in tests: no weather service is called.</summary>
public interface IWeatherProvider
{
    bool IsEnabled { get; }

    /// <summary>
    /// The forecast for <paramref name="location"/>. With <paramref name="ifModifiedSince"/> (the <c>LastModified</c> of the last answer)
    /// the service may answer that nothing changed (<see cref="WeatherAnswer.NotModified"/>). Throws <see cref="WeatherProviderException"/>.
    /// </summary>
    Task<WeatherAnswer> GetForecast(GeoLocation location, string? ifModifiedSince, CancellationToken cancellationToken);
}

/// <summary>An answer of the weather service. <c>Expires</c>: the service must not be asked again before this time.</summary>
public sealed record WeatherAnswer(bool NotModified, IReadOnlyList<WeatherPeriod> Periods, DateTimeOffset Expires, string? LastModified);

public class WeatherProviderException(string message, Exception? inner = null) : Exception(message, inner);
