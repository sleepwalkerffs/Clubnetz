using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure.Configuration.Hangfire;
using Fusonic.Extensions.Hangfire;
using Hangfire;
using Hangfire.PostgreSql;
using SimpleInjector;

namespace Bookennis.Api.Infrastructure.Configuration.Hangfire;

public static class HangfireConfiguration
{
    public static void AddHangfire(this WebApplicationBuilder builder, AppSettings appSettings, Container container)
    {
        var services = builder.Services;
        services.AddHangfire(config =>
        {
            config.UsePostgreSqlStorage(configure => configure.UseNpgsqlConnection(appSettings.ConnectionString), new PostgreSqlStorageOptions { EnableTransactionScopeEnlistment = true });
            config.UseActivator(new ContainerJobActivator(container));
            config.UseRecommendedSerializerSettings();
            config.UseSerilogLogProvider();
        });
        services.AddHangfireServer(options => options.WorkerCount = 4);
    }
}
