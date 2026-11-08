using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.Weather;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Weather;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Weather;

public class WeatherTests : TestBase
{
    private const int ClubId = TestDataSeed.ClubId;
    private static readonly GeoLocation Location = new(47.3012m, 9.6345m);

    private readonly IWeatherProvider provider;

    public WeatherTests(TestFixture fixture) : base(fixture)
    {
        provider = GetInstance<IWeatherProvider>();
        provider.ClearSubstitute(ClearOptions.All);
        provider.IsEnabled.Returns(true);
    }

    // ---------- helpers ----------

    private static DateTimeOffset StartOfHour(DateTimeOffset time) => new(time.Year, time.Month, time.Day, time.Hour, 0, 0, TimeSpan.Zero);

    private static List<WeatherPeriod> Hours(DateTimeOffset from, int count)
        => Enumerable.Range(0, count).Select(i => new WeatherPeriod(from.AddHours(i), 1, "cloudy", 10m + i, 0m, 10m, 2m)).ToList();

    private void Answers(IReadOnlyList<WeatherPeriod> periods, TimeSpan? expiresIn = null, string? lastModified = "lm")
        => provider.GetForecast(Arg.Any<GeoLocation>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                   .Returns(new WeatherAnswer(false, periods, DateTimeOffset.UtcNow + (expiresIn ?? TimeSpan.FromMinutes(30)), lastModified));

    private Task SetLocationDirectly(GeoLocation? location) => QueryAsync(async ctx =>
    {
        var club = await ctx.Clubs.SingleAsync(c => c.Id == ClubId);
        club.SetLocation(location);
        await ctx.SaveChangesAsync();
    });

    private Task<WeatherForecast?> Forecast() => QueryAsync(ctx => ctx.WeatherForecasts.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(f => f.ClubId == ClubId));

    // ---------- UpdateClubLocation ----------

    [Fact]
    public async Task UpdateClubLocation_SetsTheLocationAndFetchesTheForecast()
    {
        var now = StartOfHour(DateTimeOffset.UtcNow);
        Answers(Hours(now, 5));

        await SendAsync(new UpdateClubLocation(ClubId, 47.30124m, 9.63449m));

        var club = await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == ClubId));
        club.Latitude.Should().Be(47.3012m);
        club.Longitude.Should().Be(9.6345m);

        var forecast = await Forecast();
        forecast.Should().NotBeNull();
        forecast!.IsFor(Location).Should().BeTrue();
        forecast.Periods.Should().HaveCount(5);
        forecast.LastModified.Should().Be("lm");
        await provider.Received(1).GetForecast(Location, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateClubLocation_Cleared_RemovesTheForecast()
    {
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));

        await SendAsync(new UpdateClubLocation(ClubId, null, null));

        (await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == ClubId))).Location.Should().BeNull();
        (await Forecast()).Should().BeNull();
    }

    [Fact]
    public async Task UpdateClubLocation_Moved_ReplacesTheForecast()
    {
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));

        await SendAsync(new UpdateClubLocation(ClubId, 48.2m, 16.37m));

        var forecast = await Forecast();
        forecast!.IsFor(new GeoLocation(48.2m, 16.37m)).Should().BeTrue();
        await provider.Received(2).GetForecast(Arg.Any<GeoLocation>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateClubLocation_Unchanged_DoesNotFetchAgain()
    {
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));

        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));

        await provider.Received(1).GetForecast(Arg.Any<GeoLocation>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(91, 9)]
    [InlineData(47, 181)]
    public async Task UpdateClubLocation_OutOfRange_IsRejected(int latitude, int longitude)
    {
        var act = () => SendAsync(new UpdateClubLocation(ClubId, latitude, longitude));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(WeatherErrorCode.ClubLocationInvalid));
    }

    [Fact]
    public async Task UpdateClubLocation_OnlyOneCoordinate_IsRejected()
    {
        var act = () => SendAsync(new UpdateClubLocation(ClubId, 47m, null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(WeatherErrorCode.ClubLocationInvalid));
    }

    // ---------- RefreshClubWeather ----------

    [Fact]
    public async Task Refresh_BeforeExpires_DoesNotAskAgain()
    {
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3), TimeSpan.FromMinutes(30));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));

        await SendAsync(new RefreshClubWeather(ClubId));

        await provider.Received(1).GetForecast(Arg.Any<GeoLocation>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_AfterExpires_SendsLastModifiedAndKeepsDataWhenNotModified()
    {
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3), TimeSpan.FromSeconds(-1));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));
        provider.GetForecast(Arg.Any<GeoLocation>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new WeatherAnswer(true, [], DateTimeOffset.UtcNow.AddMinutes(30), "lm"));

        await SendAsync(new RefreshClubWeather(ClubId));

        await provider.Received(1).GetForecast(Location, "lm", Arg.Any<CancellationToken>());
        var forecast = await Forecast();
        forecast!.Periods.Should().HaveCount(3);
        forecast.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(25));
    }

    [Fact]
    public async Task Refresh_ServiceFails_KeepsTheOldForecast()
    {
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3), TimeSpan.FromSeconds(-1));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));
        provider.GetForecast(Arg.Any<GeoLocation>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(new WeatherProviderException("down"));

        await SendAsync(new RefreshClubWeather(ClubId));

        (await Forecast())!.Periods.Should().HaveCount(3);
    }

    [Fact]
    public async Task Refresh_FirstFetchFails_WaitsBeforeAskingAgainAndShowsNothing()
    {
        await SetLocationDirectly(Location);
        provider.GetForecast(Arg.Any<GeoLocation>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(new WeatherProviderException("down"));

        await SendAsync(new RefreshClubWeather(ClubId));

        var forecast = await Forecast();
        forecast!.Periods.Should().BeEmpty();
        forecast.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(20));
        (await SendAsync(new GetClubsDueForWeather())).Should().NotContain(ClubId, "a failed request is not repeated at once");
        var now = DateTimeOffset.UtcNow;
        (await SendAsync(new GetWeatherForecast(ClubId, now, now.AddDays(1)))).IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_Disabled_DoesNothing()
    {
        await SetLocationDirectly(Location);
        provider.IsEnabled.Returns(false);

        await SendAsync(new RefreshClubWeather(ClubId));

        await provider.DidNotReceiveWithAnyArgs().GetForecast(default!, default, default);
        (await Forecast()).Should().BeNull();
    }

    // ---------- GetClubsDueForWeather ----------

    [Fact]
    public async Task ClubsDue_ListsClubsWithLocationWithoutValidForecast()
    {
        (await SendAsync(new GetClubsDueForWeather())).Should().NotContain(ClubId, "the club has no location");

        await SetLocationDirectly(Location);
        (await SendAsync(new GetClubsDueForWeather())).Should().Contain(ClubId, "the club has no forecast yet");

        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3), TimeSpan.FromMinutes(30));
        await SendAsync(new RefreshClubWeather(ClubId));
        (await SendAsync(new GetClubsDueForWeather())).Should().NotContain(ClubId, "the forecast has not expired");

        await SetLocationDirectly(new GeoLocation(48m, 16m));
        (await SendAsync(new GetClubsDueForWeather())).Should().Contain(ClubId, "the forecast is for the old location");
    }

    [Fact]
    public async Task Job_RefreshesTheClubsThatAreDue()
    {
        await SetLocationDirectly(Location);
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 3));

        // The test container does not register the jobs (RecurringJobRegistrationTests checks the app)
        await ScopedAsync(() => new WeatherRefreshJob(GetInstance<IMediator>(), provider).Run());

        (await Forecast())!.Periods.Should().HaveCount(3);
    }

    // ---------- GetWeatherForecast ----------

    [Fact]
    public async Task GetWeatherForecast_NoLocation_IsNotAvailable()
    {
        var now = DateTimeOffset.UtcNow;

        var result = await SendAsync(new GetWeatherForecast(ClubId, now, now.AddDays(1)));

        result.IsAvailable.Should().BeFalse();
        result.Periods.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsThePeriodsOfTheRequestedTimeWithinTheHorizon()
    {
        var start = StartOfHour(DateTimeOffset.UtcNow);
        Answers(Hours(start, 80));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));

        var day = await SendAsync(new GetWeatherForecast(ClubId, start.AddHours(10), start.AddHours(14)));
        day.IsAvailable.Should().BeTrue();
        day.Periods.Select(p => p.From).Should().Equal(start.AddHours(10), start.AddHours(11), start.AddHours(12), start.AddHours(13));
        day.Periods[0].TemperatureCelsius.Should().Be(20m);
        day.Periods[0].Symbol.Should().Be("cloudy");

        var beyond = await SendAsync(new GetWeatherForecast(ClubId, start.AddHours(73), start.AddHours(80)));
        beyond.IsAvailable.Should().BeTrue();
        beyond.Periods.Should().BeEmpty("only the next 72 hours are shown");
    }

    [Fact]
    public async Task GetWeatherForecast_TooOld_IsNotAvailable()
    {
        Answers(Hours(StartOfHour(DateTimeOffset.UtcNow), 10));
        await SendAsync(new UpdateClubLocation(ClubId, Location.Latitude, Location.Longitude));
        await QueryAsync(ctx => ctx.Database.ExecuteSqlRawAsync(
            """UPDATE "WeatherForecasts" SET "CheckedAt" = NOW() - INTERVAL '7 hours' WHERE "ClubId" = {0}""", ClubId));

        var now = DateTimeOffset.UtcNow;
        var result = await SendAsync(new GetWeatherForecast(ClubId, now, now.AddDays(1)));

        result.IsAvailable.Should().BeFalse();
    }

    // ---------- courts ----------

    [Fact]
    public async Task UpdateCourt_SetsIndoor_AndGetCourtsReturnsIt()
    {
        var courtId = Query(ctx => ctx.TestData().Court1.Id);

        await SendAsync(new UpdateCourt(courtId, "Halle 1", "H1", null, 0, IsIndoor: true));

        var courts = await SendAsync(new GetCourts());
        courts.Courts.Single(c => c.CourtId == courtId).IsIndoor.Should().BeTrue();
        courts.Courts.Where(c => c.CourtId != courtId).Should().OnlyContain(c => !c.IsIndoor);
    }

    [Fact]
    public async Task UpdateCourt_WithoutIndoor_KeepsIt()
    {
        var courtId = Query(ctx => ctx.TestData().Court1.Id);
        await SendAsync(new UpdateCourt(courtId, "Halle 1", "H1", null, 0, IsIndoor: true));

        await SendAsync(new UpdateCourt(courtId, "Halle 1", "H1", null, 3));

        (await QueryAsync(ctx => ctx.Courts.AsNoTracking().SingleAsync(c => c.Id == courtId))).IsIndoor.Should().BeTrue();
    }

    [Fact]
    public async Task GetClubInformation_ReturnsTheLocation()
    {
        await SetLocationDirectly(Location);

        var info = await SendAsync(new GetClubInformation(ClubId));

        info.Latitude.Should().Be(47.3012m);
        info.Longitude.Should().Be(9.6345m);
    }
}
