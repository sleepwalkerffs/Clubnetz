using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bookennis.Api.Infrastructure.Data;

public class MigrationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
    {
        next(builder);
        MigrateDbContext<AppDbContext>(builder);
    };

    private static void MigrateDbContext<TDbContext>(IApplicationBuilder builder)
        where TDbContext : DbContext
    {
        using var scope = builder.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TDbContext>>();

        using var dbContext = new AppDbContext(scope.ServiceProvider.GetService<DbContextOptions<AppDbContext>>() ?? throw new InvalidOperationException("DbContextOptions<AppDbContext>"), null, null,
            null);

        if (!dbContext.Database.IsInMemory())
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(1));

            var contextName = typeof(TDbContext).Name;

            try
            {
                logger.LogInformation("Migrating database for {DbContext}", contextName);

                //DB should be created manually in dev environments or by the CI script.
                if (!dbContext.Database.GetService<IRelationalDatabaseCreator>().Exists())
                    throw new InvalidOperationException("Database does not exist");

                dbContext.Database.Migrate();

                logger.LogInformation("Migrated database for {DbContext}", contextName);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when trying to migrate {DbContext}", contextName);
                throw;
            }
        }
    }
}