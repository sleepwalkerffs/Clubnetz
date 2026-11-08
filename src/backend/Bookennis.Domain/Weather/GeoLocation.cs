using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Weather;

/// <summary>
/// Where the courts of a club are, in decimal degrees (WGS 84). Rounded to <see cref="Decimals"/> decimals (about 11 m): exact enough
/// for a weather forecast, and the weather service accepts no more.
/// </summary>
public sealed record GeoLocation
{
    public const int Decimals = 4;

    public GeoLocation(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new PreconditionException(WeatherErrorCode.ClubLocationInvalid, "The latitude must be between -90 and 90, the longitude between -180 and 180.");

        Latitude = Math.Round(latitude, Decimals, MidpointRounding.AwayFromZero);
        Longitude = Math.Round(longitude, Decimals, MidpointRounding.AwayFromZero);
    }

    public decimal Latitude { get; }
    public decimal Longitude { get; }

    /// <summary>Both or none: a club has a location or not.</summary>
    public static GeoLocation? From(decimal? latitude, decimal? longitude)
        => (latitude, longitude) switch
        {
            (null, null) => null,
            ({ } lat, { } lon) => new GeoLocation(lat, lon),
            _ => throw new PreconditionException(WeatherErrorCode.ClubLocationInvalid, "Latitude and longitude must be set together."),
        };
}
