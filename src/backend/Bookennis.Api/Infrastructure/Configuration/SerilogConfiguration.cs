using System.Globalization;
using Bookennis.Api.Infrastructure.Configuration;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class SerilogConfiguration
{
    public static void AddSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
            (_, logger) =>
            {
                logger.Enrich
                    .FromLogContext()
                    .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder().WithDefaultDestructurers().WithDestructurers(new[] { new DbUpdateExceptionDestructurer() }))
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

                // On a Windows App Service the console output goes nowhere. The log stream in the
                // Azure portal picks up text files below %HOME%\LogFiles instead.
                var home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrEmpty(home) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
                {
                    logger.WriteTo.File(
                        Path.Combine(home, "LogFiles", "Application", "app-.txt"),
                        formatProvider: CultureInfo.InvariantCulture,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        shared: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1));
                }
            }
        );

        builder.Services.AddLogging(c =>
        {
            c.AddSerilog(dispose: true);
            c.SetMinimumLevel(LogLevel.Trace);
        });
    }
}
