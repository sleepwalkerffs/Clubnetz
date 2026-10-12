using Bookennis.Client.Services;
using Bookennis.Client.Services.Store.Admin;
using Bookennis.Client.Services.Store.Badges;
using Bookennis.Client.Services.Store.Bookings;
using Bookennis.Client.Services.Store.Club;
using Bookennis.Client.Services.Store.ClubAnnouncements;
using Bookennis.Client.Services.Store.ClubApiKeys;
using Bookennis.Client.Services.Store.ClubEmailTemplates;
using Bookennis.Client.Services.Store.ClubProfile;
using Bookennis.Client.Services.Store.Family;
using Bookennis.Client.Services.Store.Guests;
using Bookennis.Client.Services.Store.Members;
using Bookennis.Client.Services.Store.Profile;
using Bookennis.Client.Services.Store.Notifications;
using Bookennis.Client.Services.Store.Legal;
using Bookennis.Client.Services.Store.Push;
using Bookennis.Client.Services.Store.Statistics;
using Bookennis.Client.Services.Store.Leaderboards;
using Bookennis.Client.Services.Store.ClubEvents;
using Bookennis.Client.Services.Store.CourtBlockings;
using Bookennis.Client.Services.Store.SubscriptionPlans;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Bookennis.Client.Infrastructure.Configuration;

public static class AddStores
{
    public static void ConfigureStores(this WebAssemblyHostBuilder builder)
    {
        builder.Services.AddSingleton<TenantProvider>();
        builder.Services.AddSingleton<ITenantProvider>(sp => sp.GetRequiredService<TenantProvider>());
        builder.Services.AddSingleton<IProfileStore, ProfileStore>();
        builder.Services.AddSingleton<IClubProfileStore, ClubProfileStore>();
        builder.Services.AddSingleton<IBookingStore, BookingStore>();
        builder.Services.AddSingleton<IMembersStore, MembersStore>();
        builder.Services.AddSingleton<IClubStore, ClubStore>();
        builder.Services.AddSingleton<IFamilyStore, FamilyStore>();
        builder.Services.AddSingleton<IGuestStore, GuestStore>();
        builder.Services.AddSingleton<IAdminStore, AdminStore>();
        builder.Services.AddSingleton<IStatisticsStore, StatisticsStore>();
        builder.Services.AddSingleton<IClubStatisticsStore, ClubStatisticsStore>();
        builder.Services.AddSingleton<ILeaderboardStore, LeaderboardStore>();
        builder.Services.AddSingleton<IBadgesStore, BadgesStore>();
        builder.Services.AddSingleton<ISubscriptionPlansStore, SubscriptionPlansStore>();
        builder.Services.AddSingleton<IClubEventsStore, ClubEventsStore>();
        builder.Services.AddSingleton<ICourtBlockingsStore, CourtBlockingsStore>();
        builder.Services.AddSingleton<IClubAnnouncementsStore, ClubAnnouncementsStore>();
        builder.Services.AddSingleton<IClubEmailTemplatesStore, ClubEmailTemplatesStore>();
        builder.Services.AddSingleton<IClubApiKeysStore, ClubApiKeysStore>();
        builder.Services.AddSingleton<IPushStore, PushStore>();
        builder.Services.AddSingleton<ILegalStore, LegalStore>();
        builder.Services.AddSingleton<INotificationPreferencesStore, NotificationPreferencesStore>();
    }
}