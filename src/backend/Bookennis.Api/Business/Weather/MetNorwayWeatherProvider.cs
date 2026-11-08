using System.Globalization;
using System.Net;
using System.Text.Json;
using Bookennis.Api.Config;
using Bookennis.Domain.Weather;

namespace Bookennis.Api.Business.Weather;

/// <summary>
/// Locationforecast 2.0 of MET Norway (<see cref="WeatherSettings"/>). Hourly for the first two to three days, then in steps of 6 hours.
/// </summary>
public sealed class MetNorwayWeatherProvider : IWeatherProvider, IDisposable
{
    /// <summary>When the answer carries no <c>Expires</c>, the service is asked again after this time.</summary>
    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(30);

    private readonly AppSettings appSettings;
    private readonly HttpClient httpClient;

    public MetNorwayWeatherProvider(AppSettings appSettings)
        : this(appSettings, new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5), AutomaticDecompression = DecompressionMethods.All })
    {
    }

    /// <summary>For tests: answers come from <paramref name="handler"/>.</summary>
    public MetNorwayWeatherProvider(AppSettings appSettings, HttpMessageHandler handler)
    {
        this.appSettings = appSettings;
        httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
    }

    private WeatherSettings Settings => appSettings.Weather;

    public bool IsEnabled => Settings.IsEnabled;

    public void Dispose() => httpClient.Dispose();

    public async Task<WeatherAnswer> GetForecast(GeoLocation location, string? ifModifiedSince, CancellationToken cancellationToken)
    {
        // At most 4 decimals, MET Norway refuses more
        var url = string.Create(CultureInfo.InvariantCulture, $"{Settings.ApiUrl}?lat={location.Latitude:0.####}&lon={location.Longitude:0.####}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", Settings.UserAgent);
        if (!string.IsNullOrEmpty(ifModifiedSince) && DateTimeOffset.TryParse(ifModifiedSince, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var since))
            request.Headers.IfModifiedSince = since;

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new WeatherProviderException("MET Norway could not be reached.", e);
        }

        using (response)
        {
            var now = DateTimeOffset.UtcNow;
            var expires = response.Content.Headers.Expires is { } expiresHeader && expiresHeader > now ? expiresHeader : now + DefaultExpiry;

            if (response.StatusCode == HttpStatusCode.NotModified)
                return new WeatherAnswer(true, [], expires, ifModifiedSince);

            if (!response.IsSuccessStatusCode)
                throw new WeatherProviderException($"MET Norway answered {(int)response.StatusCode} ({response.ReasonPhrase}).");

            var lastModified = response.Content.Headers.LastModified?.ToString("R", CultureInfo.InvariantCulture);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            try
            {
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return new WeatherAnswer(false, Parse(json.RootElement), expires, lastModified);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                throw new WeatherProviderException("MET Norway answered with invalid JSON.", e);
            }
        }
    }

    /// <summary>
    /// The periods of a Locationforecast answer: <c>next_1_hours</c> where there is one (the first days), otherwise <c>next_6_hours</c>.
    /// The temperature is the one at the start of the period (<c>instant</c>). A 6-hour period that overlaps an hourly one is skipped.
    /// </summary>
    public static List<WeatherPeriod> Parse(JsonElement root)
    {
        var periods = new List<WeatherPeriod>();
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object || !properties.TryGetProperty("timeseries", out var timeseries) || timeseries.ValueKind != JsonValueKind.Array)
            return periods;

        foreach (var entry in timeseries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("time", out var timeElement) || timeElement.ValueKind != JsonValueKind.String
                || !timeElement.TryGetDateTimeOffset(out var time) || !entry.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                continue;

            var (hours, next) = data.TryGetProperty("next_1_hours", out var next1) ? (1, next1)
                : data.TryGetProperty("next_6_hours", out var next6) ? (6, next6)
                : (0, default);
            if (hours == 0 || next.ValueKind != JsonValueKind.Object || !next.TryGetProperty("summary", out var summary)
                || summary.ValueKind != JsonValueKind.Object || !summary.TryGetProperty("symbol_code", out var symbolElement)
                || symbolElement.ValueKind != JsonValueKind.String || symbolElement.GetString() is not { Length: > 0 and <= 40 } symbol)
                continue;

            var from = time.ToUniversalTime();
            if (periods.Count > 0 && periods[^1].To > from)
                continue;

            var instant = data.TryGetProperty("instant", out var i) && i.ValueKind == JsonValueKind.Object && i.TryGetProperty("details", out var id) ? id : default;
            var details = next.TryGetProperty("details", out var d) ? d : default;

            periods.Add(new WeatherPeriod(
                from,
                hours,
                symbol,
                Number(instant, "air_temperature"),
                Number(details, "precipitation_amount"),
                Number(details, "probability_of_precipitation"),
                Number(instant, "wind_speed")));
        }

        return periods;
    }

    private static decimal? Number(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? Math.Round(number, 1)
            : null;
}
