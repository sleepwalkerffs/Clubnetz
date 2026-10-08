using Bookennis.Api.Business.Badges;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.AspNetCore.Razor;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.Common.Transactions;
using Fusonic.Extensions.Mediator;
using Fusonic.Extensions.UnitTests.EntityFrameworkCore;
using Fusonic.Extensions.UnitTests.EntityFrameworkCore.Npgsql;
using Fusonic.Extensions.UnitTests.SimpleInjector;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using SimpleInjector;

namespace Bookennis.Api.Tests;

public class TestFixture : SimpleInjectorTestFixture
{
    protected sealed override void RegisterCoreDependencies(Container container)
    {
        base.RegisterCoreDependencies(container);

        // Wire up asp.net services
        var services = new ServiceCollection();

        var appSettings = new AppSettings();
        Configuration.Bind(appSettings);
        services.Configure<AppSettings>(Configuration);
        services.AddSingleton(appSettings);

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<JsonOptions>>().Value);

        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        services.AddSingleton(env);

        services.AddLogging();
        services.AddScoped<AuthorizationHandlerService>();

        RegisterDatabase(services);

        services.AddSimpleInjector(
            container,
            setup =>
            {
                setup.AutoCrossWireFrameworkComponents = true;
                setup.AddLogging();

                container.RegisterSingleton(() => Substitute.For<IBackgroundJobClient>());
                container.RegisterSingleton(() => Substitute.For<IRazorViewRenderingService>());
                container.Register(() => Substitute.For<IHttpClientFactory>());

                container.Register<ITransactionScopeHandler, TransactionScopeHandler>();

                // Mediator
                container.RegisterMediator(services, [typeof(AppDbContext).Assembly]);

                // Auth
                container.Register(() => Substitute.For<IDomainEventDispatcher>());
                container.Register<AuthorizationHandlerService>(Lifestyle.Scoped);
                container.Register<IUserLanguageAccessor, UserLanguageAccessor>(Lifestyle.Scoped);
                container.Register(() => Substitute.For<IHttpContextAccessor>(), Lifestyle.Scoped);

                // Services
                container.Register<ITenantService, TenantService>();
                container.RegisterSingleton(() => Substitute.For<IUserAccessor>());

                // DomainServices
                container.RegisterSingleton(() => Substitute.For<IBookingDomainService>());
                container.Register<IBadgeProgressionService, BadgeProgressionService>();
                container.Register<ISubscriptionScheduler, SubscriptionScheduler>();
                container.RegisterSingleton<IClubEmailRenderer, ClubEmailRenderer>();

                // Push notifications: nothing is sent in tests
                container.RegisterSingleton(() => Substitute.For<IPushSender>());
                container.Register<IPushNotificationService, PushNotificationService>();
                container.Register<IBookingPushNotifier, BookingPushNotifier>();
                // SendEmail has no handler in tests, the emails of deleted bookings are tested with the notifier itself
                container.RegisterSingleton(() => Substitute.For<IBookingEmailNotifier>());
                container.Register<IClubEventNotifier, ClubEventNotifier>();
            }
        );

        services.AddScoped(_ => container.GetInstance<ITenantService>());
        services.AddScoped(_ => container.GetInstance<IUserAccessor>());
        services.AddScoped(_ => container.GetInstance<IMediator>());
        services.AddScoped(_ => container.GetInstance<IDomainEventDispatcher>());

        services.AddIdentity<User, UserRole>().AddEntityFrameworkStores<AppDbContext>();

        RegisterServiceProviderDependencies(services);

        services.BuildServiceProvider(validateScopes: true).UseSimpleInjector(container);
    }

    protected virtual void RegisterServiceProviderDependencies(ServiceCollection services) { }

    protected virtual string GetTestConnectionString() =>
        Configuration.GetConnectionString("Npgsql") ?? throw new PreconditionException("No connection string configured");

    private void RegisterDatabase(IServiceCollection services)
    {
        // Configure Npgsql
        var connectionString = GetTestConnectionString();

        var npgsqlTestStore = new NpgsqlDatabasePerTestStore(connectionString);
        services.AddDbContext<AppDbContext>(b => b.UseNpgsqlDatabasePerTest(npgsqlTestStore).UseProjectables());

        // Register both TestStores
        services.AddSingleton<ITestStore>(new AggregateTestStore(npgsqlTestStore));
    }
}
