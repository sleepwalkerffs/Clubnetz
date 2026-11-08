using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Weather;

namespace Bookennis.Client.Services.HttpClients.Weather;

public class WeatherHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IWeatherHttpClient
{
    public async Task<HttpResult<GetWeatherForecastResult>> GetForecast(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"?from={Uri.EscapeDataString(from.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(to.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}", cancellationToken))
            .AsHttpResult<GetWeatherForecastResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateLocation(ClubLocationModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync("location", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
