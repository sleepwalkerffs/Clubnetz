using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class DatabaseConfiguration
{
    public static void AddDatabase(this WebApplicationBuilder builder, AppSettings appSettings)
    {
        var services = builder.Services;
        services.AddDbContext<AppDbContext>(
            options => options.UseNpgsql(appSettings.ConnectionString).UseProjectables().EnableSensitiveDataLogging(builder.Environment.IsDevelopment()),
            optionsLifetime: ServiceLifetime.Scoped
        );

        services.AddTransient<IStartupFilter, MigrationStartupFilter>();
    }
}
