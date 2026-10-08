using Bookennis.Client.Services.HttpClients.Profile;
using Bookennis.Client.Services.HttpClients.Family;
using Bookennis.Client.Services;
using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Admin;
using Bookennis.Client.Services.HttpClients.Badges;
using Bookennis.Client.Services.HttpClients.Bookings;
using Bookennis.Client.Services.HttpClients.ClubProfile;
using Bookennis.Client.Services.HttpClients.Clubs;
using Bookennis.Client.Services.HttpClients.Members;
using Bookennis.Client.Services.HttpClients.Notifications;
using Bookennis.Client.Services.HttpClients.Account;
using Bookennis.Client.Services.HttpClients.Statistics;
using Bookennis.Client.Services.HttpClients.Leaderboards;
using Microsoft.AspNetCore.Components;
using Bookennis.Client.Services.HttpClients.Guests;
using Bookennis.Client.Services.HttpClients.ClubAnnouncements;
using Bookennis.Client.Services.HttpClients.ClubEvents;
using Bookennis.Client.Services.HttpClients.CourtBlockings;
using Bookennis.Client.Services.HttpClients.SubscriptionPlans;
using Bookennis.Client.Services.HttpClients.ClubEmailTemplates;
using Bookennis.Client.Services.HttpClients.Push;

namespace Bookennis.Client.Infrastructure.Configuration;

public static class AddHttpClients
{
    private static void AddDefaultHeaders(this HttpClient client) => client.DefaultRequestHeaders.Add("X-Timezone-Offset", $"{TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours}");

    public static void ConfigureHttpClients(this IServiceCollection services)
    {
        services.AddScoped<RedirectToLoginOnUnauthorizedHandler>();
        services.AddScoped<TenantDelegatingHandler>();
        services.AddHttpClient<IAccountHttpClient, AccountHttpClient>(
            (serviceProvider, client) =>
            {
                var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                client.AddDefaultHeaders();
                client.BaseAddress = new Uri(apiUrl! + "Account/");
            }
        );

        services
            .AddHttpClient<IProfileHttpClient, ProfileHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + "Profile/");
                }
            )
            .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
            .AddHttpClient<INotificationPreferencesHttpClient, NotificationPreferencesHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl!);
                }
            )
            .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
            .AddHttpClient<IPushHttpClient, PushHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + "Push/");
                }
            );

        services
           .AddHttpClient<IClubProfileHttpClient, ClubProfileHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/ClubProfile/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
            .AddHttpClient<IBookingsHttpClient, BookingsHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/Bookings/");
                }
            )
            .AddHttpMessageHandler<TenantDelegatingHandler>()
            .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
            .AddHttpClient<IClubsHttpClient, ClubsHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/");
                }
            )
            .AddHttpMessageHandler<TenantDelegatingHandler>()
            .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IMemberHttpService, MemberHttpService>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/Members/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IFamiliesHttpClient, FamiliesHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/Families/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IGuestHttpClient, GuestHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/Guests/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IAdminHttpClient, AdminHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + "Admin/");
                }
            )
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IStatisticsHttpClient, StatisticsHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/Statistics/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IClubEventsHttpClient, ClubEventsHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/ClubEvents/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<ICourtBlockingsHttpClient, CourtBlockingsHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/CourtBlockings/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IClubAnnouncementsHttpClient, ClubAnnouncementsHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/ClubAnnouncements/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<ISubscriptionPlansHttpClient, SubscriptionPlansHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/SubscriptionPlans/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IClubEmailTemplatesHttpClient, ClubEmailTemplatesHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/ClubEmailTemplates/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<ILeaderboardHttpClient, LeaderboardHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/Leaderboards/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();

        services
           .AddHttpClient<IBadgesHttpClient, BadgesHttpClient>(
                (serviceProvider, client) =>
                {
                    var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
                    client.AddDefaultHeaders();
                    client.BaseAddress = new Uri(apiUrl! + $"Clubs/{TenantDelegatingHandler.TenantPlaceholder}/");
                }
            )
           .AddHttpMessageHandler<TenantDelegatingHandler>()
           .AddHttpMessageHandler<RedirectToLoginOnUnauthorizedHandler>();
    }
}
