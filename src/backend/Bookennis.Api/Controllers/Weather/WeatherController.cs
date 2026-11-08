using Bookennis.Api.Business.Weather;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Weather;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Weather;

/// <summary>
/// The weather forecast for the location of the club (MET Norway, fetched and cached by the server). Every member who sees the booking
/// grid reads it; the club admin sets the location. Holds no personal data.
/// </summary>
public class WeatherController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    [Authorize(AuthorizationPolicies.Member)]
    public Task<GetWeatherForecastResult> GetWeatherForecast(int clubId, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken cancellationToken)
        => mediator.Send(new GetWeatherForecast(clubId, from, to), cancellationToken);

    [HttpPut("location")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task UpdateClubLocation(int clubId, [FromBody] ClubLocationModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateClubLocation(clubId, model.Latitude, model.Longitude), cancellationToken);
}
