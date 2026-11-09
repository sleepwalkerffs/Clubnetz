using Bookennis.Shared.Controller.Admin.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>Logs a signed-in user (or API key) that was refused by a policy or a resource check. Anonymous requests are not logged (401, noise).</summary>
public static class AccessDeniedAudit
{
    public const string Action = "AccessDenied";

    /// <summary>Marks an exception that is logged already (by the command decorator), so <c>ExceptionFilter</c> doesn't log it a second time.</summary>
    public const string LoggedKey = "audit.logged";

    public static Task Write(IServiceProvider services, string? reason)
    {
        var auditContext = services.GetRequiredService<IAuditContext>();
        var e = new AuditEvent
        {
            OccurredAt = DateTime.UtcNow,
            CorrelationId = auditContext.CorrelationId,
            Kind = AuditEventKind.Security,
            Action = Action,
            Outcome = AuditOutcome.Denied,
            ErrorCode = reason is { Length: > 500 } ? reason[..500] : reason,
            ClubId = auditContext.GetClubId()
        };
        auditContext.GetActor().ApplyTo(e);
        return services.GetRequiredService<IAuditWriter>().WriteAsync(e);
    }
}

/// <summary>Logs the requests that the authorization middleware forbids (a policy on the endpoint failed).</summary>
public sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var failed = authorizeResult.AuthorizationFailure?.FailedRequirements.Select(r => r.GetType().Name).Distinct();
            await AccessDeniedAudit.Write(context.RequestServices, failed is null ? null : string.Join(", ", failed));
        }

        await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
