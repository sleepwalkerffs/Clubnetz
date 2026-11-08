using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Weather;

namespace Bookennis.Client.Services.Store.Weather;

/// <summary>The weather forecast of the selected club for the next 3 days (the server keeps a copy, the browser never calls a weather service).</summary>
public interface IWeatherStore : ISemaphoreStore
{
    event Action? OnForecastChanged;

    /// <summary>The periods (an hour, further ahead 6 hours) from now on; empty when the club shows no weather.</summary>
    List<WeatherPeriodDto> Periods { get; }

    /// <summary>Loads the forecast unless it was loaded for this club a few minutes ago.</summary>
    Task LoadForecast(bool force = false);

    /// <summary>The period <paramref name="time"/> falls into, or <c>null</c>.</summary>
    WeatherPeriodDto? At(DateTimeOffset time);

    Task<HttpResult> SaveLocation(decimal? latitude, decimal? longitude);
}
