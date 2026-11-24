using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Bookennis.Api.Business.Demo;

/// <summary>Sign-ins in the public demo, where the nightly reset (<see cref="DemoReset"/>) replaces every user.</summary>
public static class DemoSignIn
{
    private static long lastResetTicks;

    /// <summary>When the data of this demo was last replaced (startup or the nightly job of this server).</summary>
    public static DateTimeOffset LastReset => new(Interlocked.Read(ref lastResetTicks), TimeSpan.Zero);

    public static void MarkReset(DateTimeOffset at) => Interlocked.Exchange(ref lastResetTicks, at.UtcTicks);

    /// <summary>
    /// Cookie validation of the demo: a cookie issued before the last reset belongs to a user that is gone, it is signed out and the
    /// visitor lands on the login page. Every other cookie passes untouched, without a database query.
    /// </summary>
    public static async Task EndWhenIssuedBeforeReset(CookieValidatePrincipalContext context)
    {
        if (context.Properties.IssuedUtc is { } issued && issued >= LastReset)
            return;

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
