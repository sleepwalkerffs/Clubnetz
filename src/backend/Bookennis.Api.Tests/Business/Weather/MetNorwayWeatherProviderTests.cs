using System.Net;
using System.Text;
using Bookennis.Api.Business.Weather;
using Bookennis.Api.Config;
using Bookennis.Domain.Weather;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Weather;

/// <summary>The client of MET Norway against canned answers: nothing leaves the machine.</summary>
public class MetNorwayWeatherProviderTests
{
    private static readonly GeoLocation Location = new(47.30125m, 9.6345m);

    /// <summary>
    /// Shortened answer of Locationforecast 2.0 (complete): two hourly entries, one entry with only <c>next_6_hours</c> that overlaps
    /// the last hour (skipped), and a 6-hour entry afterwards.
    /// </summary>
    private const string Answer = """
        {
          "type": "Feature",
          "properties": {
            "timeseries": [
              { "time": "2026-11-08T12:00:00Z", "data": {
                  "instant": { "details": { "air_temperature": 11.34, "wind_speed": 2.5 } },
                  "next_1_hours": { "summary": { "symbol_code": "partlycloudy_day" }, "details": { "precipitation_amount": 0.0, "probability_of_precipitation": 4.2 } },
                  "next_6_hours": { "summary": { "symbol_code": "cloudy" }, "details": { "precipitation_amount": 1.0 } } } },
              { "time": "2026-11-08T13:00:00Z", "data": {
                  "instant": { "details": { "air_temperature": 12.0, "wind_speed": 3.0 } },
                  "next_1_hours": { "summary": { "symbol_code": "lightrain" }, "details": { "precipitation_amount": 0.4, "probability_of_precipitation": 61.0 } } } },
              { "time": "2026-11-08T13:30:00Z", "data": {
                  "instant": { "details": { "air_temperature": 12.0 } },
                  "next_6_hours": { "summary": { "symbol_code": "rain" }, "details": {} } } },
              { "time": "2026-11-08T18:00:00Z", "data": {
                  "instant": { "details": { "air_temperature": 8.0, "wind_speed": 1.0 } },
                  "next_6_hours": { "summary": { "symbol_code": "clearsky_night" }, "details": { "precipitation_amount": 0.0, "probability_of_precipitation": 0.0 } } } },
              { "time": "2026-11-09T00:00:00Z", "data": { "instant": { "details": { "air_temperature": 5.0 } } } }
            ]
          }
        }
        """;

    private static AppSettings Settings() => new() { Weather = new WeatherSettings { ApiUrl = "https://met.test/complete", UserAgent = "Clubnetz/1.0 (+https://clubnetz.test)" } };

    [Fact]
    public async Task GetForecast_ParsesHourlyAndSixHourPeriods()
    {
        var handler = new CannedHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Answer, Encoding.UTF8, "application/json") };
            response.Content.Headers.Expires = DateTimeOffset.UtcNow.AddMinutes(20);
            response.Content.Headers.LastModified = new DateTimeOffset(2026, 11, 8, 11, 55, 0, TimeSpan.Zero);
            return response;
        });
        using var provider = new MetNorwayWeatherProvider(Settings(), handler);

        var answer = await provider.GetForecast(Location, null, CancellationToken.None);

        answer.NotModified.Should().BeFalse();
        answer.LastModified.Should().Be("Sun, 08 Nov 2026 11:55:00 GMT");
        answer.Expires.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(20), TimeSpan.FromSeconds(5));
        answer.Periods.Should().Equal(
            new WeatherPeriod(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero), 1, "partlycloudy_day", 11.3m, 0.0m, 4.2m, 2.5m),
            new WeatherPeriod(new DateTimeOffset(2026, 11, 8, 13, 0, 0, TimeSpan.Zero), 1, "lightrain", 12.0m, 0.4m, 61.0m, 3.0m),
            new WeatherPeriod(new DateTimeOffset(2026, 11, 8, 18, 0, 0, TimeSpan.Zero), 6, "clearsky_night", 8.0m, 0.0m, 0.0m, 1.0m));

        // MET Norway's terms: the app names itself and sends at most 4 decimals
        string.Join(" ", handler.Request!.Headers.GetValues("User-Agent")).Should().Be("Clubnetz/1.0 (+https://clubnetz.test)");
        handler.Request.RequestUri!.ToString().Should().Be("https://met.test/complete?lat=47.3013&lon=9.6345");
        handler.Request.Headers.IfModifiedSince.Should().BeNull();
    }

    [Fact]
    public async Task GetForecast_NotModified_SendsIfModifiedSince()
    {
        var handler = new CannedHandler(_ => new HttpResponseMessage(HttpStatusCode.NotModified));
        using var provider = new MetNorwayWeatherProvider(Settings(), handler);

        var answer = await provider.GetForecast(Location, "Sun, 08 Nov 2026 11:55:00 GMT", CancellationToken.None);

        answer.NotModified.Should().BeTrue();
        answer.Periods.Should().BeEmpty();
        answer.Expires.Should().BeAfter(DateTimeOffset.UtcNow);
        handler.Request!.Headers.IfModifiedSince.Should().Be(new DateTimeOffset(2026, 11, 8, 11, 55, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetForecast_Error_ThrowsWeatherProviderException(HttpStatusCode status)
    {
        using var provider = new MetNorwayWeatherProvider(Settings(), new CannedHandler(_ => new HttpResponseMessage(status)));

        var act = () => provider.GetForecast(Location, null, CancellationToken.None);

        await act.Should().ThrowAsync<WeatherProviderException>();
    }

    [Fact]
    public async Task GetForecast_InvalidJson_ThrowsWeatherProviderException()
    {
        using var provider = new MetNorwayWeatherProvider(Settings(), new CannedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{ not json") }));

        var act = () => provider.GetForecast(Location, null, CancellationToken.None);

        await act.Should().ThrowAsync<WeatherProviderException>();
    }

    [Fact]
    public async Task GetForecast_UnexpectedShape_SkipsTheEntries()
    {
        const string odd = """{ "properties": { "timeseries": [ { "time": "2026-11-08T12:00:00Z", "data": { "next_1_hours": { "summary": { "symbol_code": 5 } } } }, 7 ] } }""";
        using var provider = new MetNorwayWeatherProvider(Settings(), new CannedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(odd) }));

        var answer = await provider.GetForecast(Location, null, CancellationToken.None);

        answer.Periods.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForecast_Unreachable_ThrowsWeatherProviderException()
    {
        using var provider = new MetNorwayWeatherProvider(Settings(), new CannedHandler(_ => throw new HttpRequestException("no route")));

        var act = () => provider.GetForecast(Location, null, CancellationToken.None);

        await act.Should().ThrowAsync<WeatherProviderException>();
    }

    private sealed class CannedHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(answer(request));
        }
    }
}
