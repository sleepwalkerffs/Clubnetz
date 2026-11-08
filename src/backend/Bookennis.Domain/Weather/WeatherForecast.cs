using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Weather;

/// <summary>
/// The weather forecast for the location of a club, as the background job fetched it last (one row per club). Members only ever read
/// this copy: their browsers never contact the weather service, and opening the booking grid costs no request.
/// </summary>
[Guid("5B7E2C94-3F1A-4D6B-8E25-9C0A4F7D1B63")]
public class WeatherForecast : TenantDomainEntity, IAggregateRoot
{
    /// <summary>How far ahead the forecast is kept and shown.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromHours(72);

    /// <summary>A forecast that could not be refreshed for this long is not shown any more.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);

    public const int MaxLastModifiedLength = 100;

#pragma warning disable CS8618
    private WeatherForecast() { }
#pragma warning restore CS8618

    public WeatherForecast(int clubId, GeoLocation location)
    {
        ClubId = clubId;
        Latitude = location.Latitude;
        Longitude = location.Longitude;
        Periods = [];
    }

    /// <summary>The location the forecast is for; when the club moves its location the forecast is thrown away.</summary>
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }

    /// <summary>When the weather service last answered (new data or "not modified"). <c>null</c> before the first answer.</summary>
    public DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>The weather service must not be asked again before this time.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>The <c>Last-Modified</c> header of the last answer, sent back as <c>If-Modified-Since</c>.</summary>
    public string? LastModified { get; private set; }

    public List<WeatherPeriod> Periods { get; private set; }

    public bool IsFor(GeoLocation location) => Latitude == location.Latitude && Longitude == location.Longitude;

    public bool IsDue(DateTimeOffset now) => ExpiresAt is null || ExpiresAt <= now;

    public bool IsCurrent(DateTimeOffset now) => CheckedAt is { } checkedAt && checkedAt > now - MaxAge;

    /// <summary>New data: only the periods that are not over and start within the horizon are kept.</summary>
    public void Update(IEnumerable<WeatherPeriod> periods, DateTimeOffset now, DateTimeOffset expiresAt, string? lastModified)
    {
        Periods = periods
            .Where(p => p.To > now && p.From < now + Horizon)
            .OrderBy(p => p.From)
            .ToList();
        Checked(now, expiresAt, lastModified);
    }

    /// <summary>The weather service had nothing new.</summary>
    public void NotModified(DateTimeOffset now, DateTimeOffset expiresAt)
        => Checked(now, expiresAt, LastModified);

    /// <summary>The weather service failed: the old periods stay, the service is asked again at <paramref name="retryAt"/>.</summary>
    public void Failed(DateTimeOffset retryAt) => ExpiresAt = retryAt;

    private void Checked(DateTimeOffset now, DateTimeOffset expiresAt, string? lastModified)
    {
        CheckedAt = now;
        ExpiresAt = expiresAt;
        LastModified = lastModified is { Length: > MaxLastModifiedLength } ? null : lastModified;
    }
}

/// <summary>
/// The weather of one period: an hour in the first days, later 6 hours (the weather service gets coarser further ahead).
/// <see cref="Symbol"/> is the symbol code of the weather service, e.g. <c>partlycloudy_day</c> or <c>lightrain</c>.
/// </summary>
public sealed record WeatherPeriod(
    DateTimeOffset From,
    int Hours,
    string Symbol,
    decimal? TemperatureCelsius,
    decimal? PrecipitationMm,
    decimal? PrecipitationProbability,
    decimal? WindSpeedMs)
{
    [JsonIgnore]
    public DateTimeOffset To => From.AddHours(Hours);
}
