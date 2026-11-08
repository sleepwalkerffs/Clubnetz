using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Weather;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Weather;

public class WeatherForecastTests
{
    private const int ClubId = 7;
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly GeoLocation Location = new(47.3012m, 9.6345m);

    private static WeatherPeriod Period(DateTimeOffset from, int hours = 1) => new(from, hours, "cloudy", 12.5m, 0.2m, 30m, 3.1m);

    [Fact]
    public void GeoLocation_RoundsToFourDecimals()
    {
        var location = new GeoLocation(47.301249m, 9.634551m);

        location.Latitude.Should().Be(47.3012m);
        location.Longitude.Should().Be(9.6346m);
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    public void GeoLocation_OutOfRange_Throws(double latitude, double longitude)
    {
        var act = () => new GeoLocation((decimal)latitude, (decimal)longitude);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(WeatherErrorCode.ClubLocationInvalid));
    }

    [Fact]
    public void GeoLocation_From_NeedsBothOrNone()
    {
        GeoLocation.From(null, null).Should().BeNull();
        GeoLocation.From(47m, 9m).Should().Be(new GeoLocation(47m, 9m));

        var act = () => GeoLocation.From(47m, null);
        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(WeatherErrorCode.ClubLocationInvalid));
    }

    [Fact]
    public void Club_SetLocation_StoresAndClears()
    {
        var club = new Bookennis.Domain.Clubs.Club("Club", new(new TimeOnly(7, 0), new TimeOnly(22, 0)), new(new TimeOnly(17, 0), new TimeOnly(20, 0)), []);

        club.SetLocation(Location);
        club.Location.Should().Be(Location);

        club.SetLocation(null);
        club.Location.Should().BeNull();
        club.Latitude.Should().BeNull();
    }

    [Fact]
    public void Update_KeepsOnlyPeriodsWithinTheHorizonAndNotOver()
    {
        var forecast = new WeatherForecast(ClubId, Location);

        forecast.Update(
        [
            Period(Now.AddHours(-2)), // over
            Period(Now.AddMinutes(-30)), // running
            Period(Now.AddHours(1)),
            Period(Now.AddHours(71), 6), // starts within the horizon
            Period(Now.AddHours(72)), // starts at the horizon
        ], Now, Now.AddMinutes(30), "Sun, 08 Nov 2026 11:55:00 GMT");

        forecast.Periods.Select(p => p.From).Should().Equal(Now.AddMinutes(-30), Now.AddHours(1), Now.AddHours(71));
        forecast.CheckedAt.Should().Be(Now);
        forecast.ExpiresAt.Should().Be(Now.AddMinutes(30));
        forecast.LastModified.Should().Be("Sun, 08 Nov 2026 11:55:00 GMT");
    }

    [Fact]
    public void NotModified_KeepsThePeriodsAndMovesTheTimes()
    {
        var forecast = new WeatherForecast(ClubId, Location);
        forecast.Update([Period(Now.AddHours(1))], Now, Now.AddMinutes(30), "lm");

        forecast.NotModified(Now.AddHours(1), Now.AddHours(2));

        forecast.Periods.Should().HaveCount(1);
        forecast.CheckedAt.Should().Be(Now.AddHours(1));
        forecast.ExpiresAt.Should().Be(Now.AddHours(2));
        forecast.LastModified.Should().Be("lm");
    }

    [Fact]
    public void IsDueAndIsCurrent_FollowTheTimes()
    {
        var forecast = new WeatherForecast(ClubId, Location);
        forecast.IsDue(Now).Should().BeTrue();
        forecast.IsCurrent(Now).Should().BeFalse();

        forecast.Update([], Now, Now.AddMinutes(30), null);

        forecast.IsDue(Now.AddMinutes(29)).Should().BeFalse();
        forecast.IsDue(Now.AddMinutes(30)).Should().BeTrue();
        forecast.IsCurrent(Now + WeatherForecast.MaxAge - TimeSpan.FromMinutes(1)).Should().BeTrue();
        forecast.IsCurrent(Now + WeatherForecast.MaxAge).Should().BeFalse();
    }

    [Fact]
    public void IsFor_ComparesTheLocation()
    {
        var forecast = new WeatherForecast(ClubId, Location);

        forecast.IsFor(new GeoLocation(47.3012m, 9.6345m)).Should().BeTrue();
        forecast.IsFor(new GeoLocation(47.3013m, 9.6345m)).Should().BeFalse();
    }
}
