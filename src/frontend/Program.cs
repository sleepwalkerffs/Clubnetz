using System.Text.Json;
using Bookennis.Client;
using Bookennis.Client.Infrastructure.Configuration;
using Bookennis.Client.Services;
using Bookennis.Client.Services.AppVersion;
using Bookennis.Client.Services.Legal;
using Bookennis.Client.Services.Theme;
using Bookennis.Client.Services.UserAccessor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

#pragma warning disable CA1852

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(_ => new JsonSerializerOptions().ConfigureJsonOptions());
builder.Services.AddSingleton<IAppVersionService, AppVersionService>();
builder.Services.AddTransient<AppVersionHandler>();
builder.Services.ConfigureHttpClientDefaults(http => http.AddHttpMessageHandler<AppVersionHandler>());
builder.Services.ConfigureHttpClients();
builder.Services.AddLocalization();
builder.ConfigureStores();
builder.Services.AddSingleton<IThemeService, ThemeService>();
builder.Services.AddSingleton(LegalSettings.FromConfiguration(builder.Configuration.GetSection("Legal")));

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 1000;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

//builder.Services.AddApiAuthorization();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddScoped<IAuthStateChanged>(provider => provider.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddScoped<IClaimsUserDataAccessor>(provider => provider.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddCascadingAuthenticationState();

//builder.Services.AddSingleton<ApiAuthenticationStateProvider>();
//builder.Services.AddSingleton<AuthenticationStateProvider>(provider => provider.GetRequiredService<ApiAuthenticationStateProvider>());

//builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var app = builder.Build();
await app.RunAsync();
