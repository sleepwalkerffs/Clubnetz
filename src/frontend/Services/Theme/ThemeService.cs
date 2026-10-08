using Microsoft.JSInterop;
using MudBlazor;

namespace Bookennis.Client.Services.Theme;

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

public interface IThemeService
{
    ThemeMode Mode { get; }
    bool IsDarkMode { get; }
    event Action? OnThemeChanged;

    /// <summary>Reads the stored mode and starts following the OS preference. Call once the provider has rendered.</summary>
    Task InitializeAsync(MudThemeProvider themeProvider);

    Task SetModeAsync(ThemeMode mode);
}

/// <summary>
/// Holds the light/dark mode choice. The choice is stored per browser in localStorage (see wwwroot/theme.js,
/// which also applies it before Blazor has started to avoid a light flash).
/// </summary>
public class ThemeService(IJSRuntime jsRuntime) : IThemeService
{
    private bool systemIsDark;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;
    public bool IsDarkMode => Mode == ThemeMode.Dark || (Mode == ThemeMode.System && systemIsDark);

    public event Action? OnThemeChanged;

    public async Task InitializeAsync(MudThemeProvider themeProvider)
    {
        var storedMode = await jsRuntime.InvokeAsync<string?>("App.theme.getMode");
        Mode = Enum.TryParse<ThemeMode>(storedMode, ignoreCase: true, out var mode) ? mode : ThemeMode.System;
        systemIsDark = await themeProvider.GetSystemDarkModeAsync();
        await themeProvider.WatchSystemDarkModeAsync(OnSystemDarkModeChanged);
        await ApplyAsync();
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        Mode = mode;
        await jsRuntime.InvokeVoidAsync("App.theme.setMode", mode.ToString().ToLowerInvariant());
        await ApplyAsync();
    }

    private async Task OnSystemDarkModeChanged(bool isDark)
    {
        systemIsDark = isDark;
        await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        await jsRuntime.InvokeVoidAsync("App.theme.apply", IsDarkMode);
        OnThemeChanged?.Invoke();
    }
}
