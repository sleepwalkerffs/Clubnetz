using Bookennis.Api.Business.Badges;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.SubscriptionPlans;
using Fusonic.Extensions.AspNetCore.Http;
using Fusonic.Extensions.Common.Security;
using SimpleInjector;

namespace Bookennis.Api.Infrastructure.Configuration.ContainerRegistrations;

public static class ServicesConfiguration
{
    public static void AddServices(this Container container)
    {
        container.Register<IDomainEventDispatcher, DomainEventDispatcher>();
        container.Register<IUserAccessor, HttpContextUserAccessor>();
        container.Register<IUserLanguageAccessor, UserLanguageAccessor>();

        container.Register<ITenantService, TenantService>();
        container.Register<MultiTenantServiceMiddleware>();

        container.Register<IBookingDomainService, BookingDomainService>();
        container.Register<IBadgeProgressionService, BadgeProgressionService>();
        container.Register<ISubscriptionScheduler, SubscriptionScheduler>();
        container.RegisterSingleton<IClubEmailRenderer, ClubEmailRenderer>();

        container.RegisterSingleton<IPushSender, WebPushSender>();
        container.Register<IPushNotificationService, PushNotificationService>();
        container.Register<IBookingPushNotifier, BookingPushNotifier>();
        container.Register<IBookingEmailNotifier, BookingEmailNotifier>();
        container.Register<IClubEventNotifier, ClubEventNotifier>();
        container.Register<BookingReminderJob>();
        container.Register<ClubEventReminderJob>();
    }
}
