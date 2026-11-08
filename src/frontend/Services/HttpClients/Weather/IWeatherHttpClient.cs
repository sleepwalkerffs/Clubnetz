using Bookennis.Shared.Controller.Weather;

namespace Bookennis.Client.Services.HttpClients.Weather;

public interface IWeatherHttpClient
{
    public Task<HttpResult<GetWeatherForecastResult>> GetForecast(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateLocation(ClubLocationModel model, CancellationToken cancellationToken = default);
}
