using Bookennis.Api.Data;
using Bookennis.Domain.Weather;
using Bookennis.Shared.Controller.Weather;
using Fusonic.Extensions.EntityFrameworkCore;
using Fusonic.Extensions.Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Weather;

/// <summary>
/// The club admin sets (or clears) where the courts are. A new location throws the old forecast away and fetches one in the
/// background right away, so members see the weather without waiting for the job.
/// </summary>
public record UpdateClubLocation(int ClubId, decimal? Latitude, decimal? Longitude) : ICommand
{
    public class Handler(AppDbContext context, IMediator mediator) : IRequestHandler<UpdateClubLocation>
    {
        public async Task<Unit> Handle(UpdateClubLocation request, CancellationToken cancellationToken)
        {
            var location = GeoLocation.From(request.Latitude, request.Longitude);
            var club = await context.Clubs.SingleRequiredAsync(c => c.Id == request.ClubId, cancellationToken);
            if (club.Location == location)
                return default;

            club.SetLocation(location);
            var forecast = await context.WeatherForecasts.FirstOrDefaultAsync(f => f.ClubId == request.ClubId, cancellationToken);
            if (forecast is not null)
                context.WeatherForecasts.Remove(forecast);

            await context.SaveChangesAsync(cancellationToken);

            if (location is not null)
                await mediator.Send(new RefreshClubWeather(request.ClubId), cancellationToken);

            return default;
        }
    }
}

/// <summary>
/// Fetches the forecast of one club, unless the weather service asked not to be asked again yet. When the service fails, the old
/// forecast stays (it is hidden once it is older than <see cref="WeatherForecast.MaxAge"/>) and the job tries again later.
/// </summary>
public record RefreshClubWeather(int ClubId) : ICommand
{
    // A background job: the request that sets the location and the recurring job never wait for the weather service
    [OutOfBand]
    public class Handler(AppDbContext context, IWeatherProvider provider, ILogger<RefreshClubWeather> logger) : IRequestHandler<RefreshClubWeather>
    {
        public async Task<Unit> Handle(RefreshClubWeather request, CancellationToken cancellationToken)
        {
            if (!provider.IsEnabled)
                return default;

            // A background job has no tenant: the club is filtered explicitly
            var club = await context.Clubs.IgnoreQueryFilters()
                .Where(c => c.Id == request.ClubId)
                .Select(c => new { c.Latitude, c.Longitude })
                .FirstOrDefaultAsync(cancellationToken);
            var forecast = await context.WeatherForecasts.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.ClubId == request.ClubId, cancellationToken);
            var location = club is null ? null : GeoLocation.From(club.Latitude, club.Longitude);

            if (forecast is not null && (location is null || !forecast.IsFor(location)))
            {
                context.WeatherForecasts.Remove(forecast);
                forecast = null;
            }

            if (location is null)
            {
                await context.SaveChangesAsync(cancellationToken);
                return default;
            }

            var now = DateTimeOffset.UtcNow;
            if (forecast is not null && !forecast.IsDue(now))
            {
                await context.SaveChangesAsync(cancellationToken);
                return default;
            }

            var isNew = forecast is null;
            forecast ??= new WeatherForecast(request.ClubId, location);

            try
            {
                var answer = await provider.GetForecast(location, forecast.LastModified, cancellationToken);
                if (answer.NotModified)
                    forecast.NotModified(now, answer.Expires);
                else
                    forecast.Update(answer.Periods, now, answer.Expires, answer.LastModified);
            }
            catch (WeatherProviderException e)
            {
                // The old forecast stays (a new row has none and is not shown); the service is asked again after a pause, not every run
                logger.LogWarning(e, "The weather forecast of club {ClubId} could not be fetched.", request.ClubId);
                forecast.Failed(now + MetNorwayWeatherProvider.DefaultExpiry);
            }

            if (isNew)
                context.WeatherForecasts.Add(forecast);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e)
            {
                // Another refresh of the club (the job and a new location at once) saved first: its forecast is just as new
                logger.LogInformation(e, "The weather forecast of club {ClubId} was refreshed at the same time.", request.ClubId);
            }

            return default;
        }
    }
}

/// <summary>The clubs with a location whose forecast is missing or may be fetched again (all clubs).</summary>
public record GetClubsDueForWeather : IQuery<List<int>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubsDueForWeather, List<int>>
    {
        public Task<List<int>> Handle(GetClubsDueForWeather request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;

            // A background job has no tenant: every club
            return context.Clubs.IgnoreQueryFilters()
                .Where(c => c.Latitude != null && c.Longitude != null)
                .Where(c => !context.WeatherForecasts.IgnoreQueryFilters().Any(f => f.ClubId == c.Id
                                                                                    && f.Latitude == c.Latitude
                                                                                    && f.Longitude == c.Longitude
                                                                                    && f.ExpiresAt != null
                                                                                    && f.ExpiresAt > now))
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }
    }
}

/// <summary>
/// The forecast for the booking grid: the periods that overlap <c>From</c>-<c>To</c>, are not over and start within the next
/// <see cref="WeatherForecast.Horizon"/>. Not available without a location or when the forecast is too old.
/// </summary>
public record GetWeatherForecast(int ClubId, DateTimeOffset From, DateTimeOffset To) : IQuery<GetWeatherForecastResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetWeatherForecast, GetWeatherForecastResult>
    {
        public async Task<GetWeatherForecastResult> Handle(GetWeatherForecast request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Where(c => c.Id == request.ClubId)
                .Select(c => new { c.Latitude, c.Longitude })
                .SingleRequiredAsync(cancellationToken);
            var forecast = await context.WeatherForecasts.AsNoTracking()
                .FirstOrDefaultAsync(f => f.ClubId == request.ClubId, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var location = GeoLocation.From(club.Latitude, club.Longitude);
            if (location is null || forecast is null || !forecast.IsFor(location) || !forecast.IsCurrent(now))
                return new GetWeatherForecastResult { IsAvailable = false, Periods = [] };

            var horizon = now + WeatherForecast.Horizon;
            var periods = forecast.Periods
                .Where(p => p.To > now && p.From < horizon && p.To > request.From && p.From < request.To)
                .Select(p => new WeatherPeriodDto
                {
                    From = p.From,
                    Hours = p.Hours,
                    Symbol = p.Symbol,
                    TemperatureCelsius = p.TemperatureCelsius,
                    PrecipitationMm = p.PrecipitationMm,
                    PrecipitationProbability = p.PrecipitationProbability,
                    WindSpeedMs = p.WindSpeedMs,
                })
                .ToList();

            return new GetWeatherForecastResult { IsAvailable = true, Periods = periods };
        }
    }
}

/// <summary>Entry point of the recurring Hangfire job: every club is refreshed in its own background job, a failure only skips that one.</summary>
public class WeatherRefreshJob(IMediator mediator, IWeatherProvider provider)
{
    public const string JobId = "weather-refresh";

    /// <summary>Not on the full hour: MET Norway asks to spread the requests.</summary>
    public const string Schedule = "7,22,37,52 * * * *";

    public async Task Run()
    {
        if (!provider.IsEnabled)
            return;

        foreach (var clubId in await mediator.Send(new GetClubsDueForWeather()))
            await mediator.Send(new RefreshClubWeather(clubId));
    }
}
