using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Weather;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Client.Services.Store.ClubProfile;
using Bookennis.Shared.Controller.Weather;

namespace Bookennis.Client.Services.Store.Weather;

public class WeatherStore(IWeatherHttpClient httpClient, IClubProfileStore clubProfileStore) : SemaphoreStore, IWeatherStore
{
    /// <summary>The server refreshes the forecast every 15 to 30 minutes; loading it more often shows nothing new.</summary>
    private static readonly TimeSpan ReloadAfter = TimeSpan.FromMinutes(10);

    private int? loadedClubId;
    private DateTimeOffset loadedAt;

    public event Action? OnForecastChanged;

    public List<WeatherPeriodDto> Periods { get; private set; } = [];

    public async Task LoadForecast(bool force = false)
    {
        var clubId = clubProfileStore.SelectedClubId;
        if (!force && loadedClubId == clubId && DateTimeOffset.UtcNow - loadedAt < ReloadAfter)
            return;

        // Never show the weather of the club selected before, also when the request fails
        if (loadedClubId != clubId)
        {
            Periods = [];
            loadedClubId = null;
        }

        await RunInLoadingContextAsync(async cancellationToken =>
        {
            var now = DateTimeOffset.UtcNow;
            var response = await httpClient.GetForecast(now, now.AddDays(4), cancellationToken);
            if (response is { Success: true, Dto: not null })
            {
                Periods = response.Dto.IsAvailable ? response.Dto.Periods : [];
                loadedClubId = clubId;
                loadedAt = now;
                OnForecastChanged?.Invoke();
            }
        }, nameof(LoadForecast));
    }

    public WeatherPeriodDto? At(DateTimeOffset time) => Periods.FirstOrDefault(p => p.Contains(time));

    public Task<HttpResult> SaveLocation(decimal? latitude, decimal? longitude) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await httpClient.UpdateLocation(new ClubLocationModel { Latitude = latitude, Longitude = longitude }, cancellationToken);
        if (result.Success)
        {
            // The forecast of the new location is fetched in the background: the next load shows it
            Periods = [];
            loadedClubId = null;
            OnForecastChanged?.Invoke();
        }

        return result;
    }, nameof(SaveLocation));
}
