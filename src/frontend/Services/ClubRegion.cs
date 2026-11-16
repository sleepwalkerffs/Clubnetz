using System.Globalization;
using System.Text.Json;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils;
using Microsoft.JSInterop;

namespace Bookennis.Client.Services;

/// <summary>
/// Switches the whole app to the clock and the formats of the open club, whatever the device's time zone and region are:
/// a member of a club in Virginia who opens the app in Vienna still sees the court times of Virginia, written the American way.
/// <list type="bullet">
/// <item><b>Time zone</b>: the WebAssembly runtime reads the <c>TZ</c> environment variable for <see cref="TimeZoneInfo.Local"/>
/// (dotnet.js sets it from the browser at start). Setting it to the club's IANA id and clearing the cache makes
/// <see cref="TimeZoneInfo.Local"/>, <see cref="DateTime.Now"/>, <see cref="DateTimeOffset.ToLocalTime()"/> and so the JSON
/// converter (<c>DateTimeOffsetJsonConverter</c>) use the club's time. Checked in a .NET 10 Blazor WebAssembly app.</item>
/// <item><b>Formats</b>: <see cref="CultureInfo.DefaultThreadCurrentCulture"/> becomes a <see cref="RegionalCulture"/> (the browser's
/// language with the club's country and currency). The default is what every later render and event handler reads: setting
/// only <see cref="CultureInfo.CurrentCulture"/> inside an async method is undone when the method returns (it flows with the
/// execution context). The UI culture (which texts are shown) stays the browser's.</item>
/// </list>
/// The last club's region is kept in localStorage and applied when the app starts (<see cref="ApplyStored"/>), so the pages
/// don't flash in the wrong formats or times until the club profile has loaded. Data loaded before a switch keeps the old
/// offsets: switching clubs reloads the page (<c>NavigationManagerExtensions.SwitchToClub</c>), and the club pages render only
/// after the club profile (<c>ClubWithStoreLayout</c>).
/// </summary>
public static class ClubRegion
{
    /// <summary>The club's time zone the app runs on, <c>null</c> while it runs on the browser's (stored in localStorage by <c>App.clubRegion</c> in theme.js).</summary>
    public static string? TimeZoneId { get; private set; }

    /// <summary>Applies the region of the last club opened on this device. Call once at start, before the first page renders.</summary>
    public static void ApplyStored(IJSRuntime js)
    {
        try
        {
            if (js is not IJSInProcessRuntime inProcess)
                return;

            var json = inProcess.Invoke<string?>("App.clubRegion.get");
            if (string.IsNullOrEmpty(json))
                return;

            var stored = JsonSerializer.Deserialize<StoredRegion>(json);
            if (stored is not null)
                ApplyRegion(stored.TimeZoneId, stored.Country, stored.Currency, setCurrent: false);
        }
        catch (Exception)
        {
            // Storage blocked or an old value: the club profile sets the region once it has loaded
        }
    }

    /// <summary>Switches the app to the club's time zone and formats and remembers them for the next start.</summary>
    public static void Apply(IJSRuntime js, string timeZoneId, Country country, string currency)
    {
        ApplyRegion(timeZoneId, country, currency);

        try
        {
            if (js is IJSInProcessRuntime inProcess)
                inProcess.InvokeVoid("App.clubRegion.set", JsonSerializer.Serialize(new StoredRegion(timeZoneId, country, currency)));
        }
        catch (Exception)
        {
            // Storage blocked (e.g. private mode): the region is set again when the club profile loads
        }
    }

    /// <param name="setCurrent">
    /// Also set <see cref="CultureInfo.CurrentCulture"/> of the current flow. Not at start: an explicit culture flows into everything
    /// started from there (loading the profile, the first renders) and would hide the club's culture set later as the default.
    /// </param>
    private static void ApplyRegion(string timeZoneId, Country country, string currency, bool setCurrent = true)
    {
        SetTimeZone(timeZoneId);

        var culture = RegionalCulture.For(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de", country, currency);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        // Also for the code that runs on in the current flow (an explicitly set culture wins over the default)
        if (setCurrent)
            CultureInfo.CurrentCulture = culture;
    }

    private static void SetTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId == TimeZoneId)
            return;

        var previous = Environment.GetEnvironmentVariable("TZ");
        Environment.SetEnvironmentVariable("TZ", timeZoneId);
        TimeZoneInfo.ClearCachedData();

        // An unknown id makes the runtime fall back to UTC: keep the browser's zone then
        if (TimeZoneInfo.Local.Id != timeZoneId)
        {
            Environment.SetEnvironmentVariable("TZ", previous);
            TimeZoneInfo.ClearCachedData();
            return;
        }

        TimeZoneId = timeZoneId;
    }

    private sealed record StoredRegion(string TimeZoneId, Country Country, string Currency);
}
