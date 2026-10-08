using System.Reflection;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class AppVersionConfiguration
{
    // "1.0.0+<commit>". API and client are built from the same commit, so both report the same value.
    public static readonly string Version = typeof(AppVersionConfiguration).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    /// <summary>
    /// Adds the app version to every API response, so a client that is still running an older build
    /// (e.g. an installed app that was resumed after a deployment) can offer a reload.
    /// </summary>
    public static void UseAppVersionHeader(this WebApplication app)
        => app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[AppVersionHeader.Name] = Version;
                    return Task.CompletedTask;
                });
            }

            await next();
        });
}
