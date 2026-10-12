using System.Net;
using System.Threading.RateLimiting;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Shared.Controller.Shared;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookennis.Api.Infrastructure.Configuration;

/// <summary>
/// Limits the anonymous account endpoints (login, registration, password reset, ...) per client IP to slow down brute force and
/// email flooding. Use <see cref="EnableRateLimitingAttribute"/> with one of the policy names on the controller actions.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>Endpoints that check credentials or tokens (login, guest login, reset password, confirm email).</summary>
    public const string AuthenticationPolicy = "Authentication";

    /// <summary>Endpoints that send an email to an address given in the request (register, forgot password, change email).</summary>
    public const string EmailPolicy = "Email";

    public const string TooManyRequestsErrorCode = "TooManyRequests";

    /// <summary>How many requests a single club API key may send per minute.</summary>
    public const int ClubApiKeyRequestsPerMinute = 120;

    public static void AddRateLimiting(this WebApplicationBuilder builder)
    {
        // Azure App Service terminates TLS in front of the app and appends the client IP to X-Forwarded-For. Only the last
        // (rightmost) entry is trusted, so a client can't bypass the limits with a forged header.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;

            // Requests made with a club API key are limited per key on every endpoint, signed-in users are not limited here
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.User.GetClubApiKeyId() is { } keyId
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        $"club-api-key:{keyId}",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = ClubApiKeyRequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter(""));

            options.AddPolicy(AuthenticationPolicy, context => PerClientIp(context, permitLimit: 10, window: TimeSpan.FromMinutes(1)));
            options.AddPolicy(EmailPolicy, context => PerClientIp(context, permitLimit: 5, window: TimeSpan.FromMinutes(15)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorCodeResponse("Too many requests. Please try again later.", TooManyRequestsErrorCode, null),
                    cancellationToken);
            };
        });
    }

    private static RateLimitPartition<string> PerClientIp(HttpContext context, int permitLimit, TimeSpan window)
        => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 });
}
