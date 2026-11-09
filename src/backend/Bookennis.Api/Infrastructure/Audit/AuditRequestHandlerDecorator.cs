using System.Diagnostics;
using System.Reflection;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Admin.Audit;
using Fusonic.Extensions.Common.Entities;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>
/// Logs every top-level command (and the queries marked with <see cref="AuditedReadAttribute"/>): who ran it, whether it worked, the
/// error code and how long it took. The outermost decorator, so it sees the outcome after the transaction is committed or rolled back,
/// and writes with <see cref="IAuditWriter"/> outside of it. The request itself (the payload) is never stored.
/// Commands sent by another command are part of it and not logged on their own. Background commands that succeed and change nothing
/// (most runs of the jobs) are left out.
/// </summary>
public sealed class AuditRequestHandlerDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IAuditContext auditContext,
    IAuditWriter auditWriter) : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly AuditEventKind? kind = GetKind(typeof(TRequest));

    private static readonly PropertyInfo? userIdProperty = typeof(TRequest).GetProperty("UserId") is { PropertyType: var t } p && t == typeof(int) ? p : null;

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        if (kind is null || AuditCommandDepth.Current > 0)
            return await inner.Handle(request, cancellationToken);

        AuditCommandDepth.Current++;
        auditContext.ResetSubject();
        var changesBefore = auditContext.DataChangeCount;
        // Who made the request, taken before the handler runs: signing in or out replaces the user of the request
        var actor = auditContext.GetActor();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await inner.Handle(request, cancellationToken);
            stopwatch.Stop();

            var changedNothing = auditContext.DataChangeCount == changesBefore;
            if (!(actor.System is not null && changedNothing && kind == AuditEventKind.Command))
                await Write(request, actor, AuditOutcome.Succeeded, null, stopwatch);

            foreach (var userId in auditContext.TakeUsersToPseudonymize())
                await auditWriter.PseudonymizeUserAsync(userId, CancellationToken.None);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var (outcome, errorCode) = AuditOutcomes.Classify(ex);
            await Write(request, actor, outcome, errorCode, stopwatch);
            ex.Data[AccessDeniedAudit.LoggedKey] = true;
            throw;
        }
        finally
        {
            AuditCommandDepth.Current--;
        }
    }

    private Task Write(TRequest request, AuditActor actor, AuditOutcome outcome, string? errorCode, Stopwatch stopwatch)
    {
        // Who a security command is about: what the handler said, otherwise the UserId of the request (an id, not the payload)
        var subject = auditContext.Subject;
        if (subject.UserId is null && kind == AuditEventKind.Security && userIdProperty?.GetValue(request) is int requestUserId)
            subject = (requestUserId, null);

        var e = new AuditEvent
        {
            OccurredAt = DateTime.UtcNow,
            CorrelationId = auditContext.CorrelationId,
            Kind = kind!.Value,
            Action = typeof(TRequest).Name,
            Outcome = outcome,
            ErrorCode = errorCode,
            ClubId = auditContext.GetClubId(),
            SubjectUserId = subject.UserId,
            SubjectName = subject.Name,
            DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)
        };
        actor.ApplyTo(e);
        return auditWriter.WriteAsync(e);
    }

    private static AuditEventKind? GetKind(Type requestType)
    {
        if (requestType.GetCustomAttribute<NotAuditedAttribute>() is not null)
            return null;
        if (requestType.GetCustomAttribute<AuditedReadAttribute>() is not null)
            return AuditEventKind.Read;
        if (IsRead(requestType))
            return null;
        return requestType.GetCustomAttribute<SecurityAuditAttribute>() is not null ? AuditEventKind.Security : AuditEventKind.Command;
    }

    /// <summary>A query, or one of the older reads that are declared as <c>ICommand&lt;T&gt;</c> (<c>GetCourts</c>, <c>GetMember</c>, ...).</summary>
    private static bool IsRead(Type requestType) =>
        requestType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>))
        || requestType.Name.StartsWith("Get", StringComparison.Ordinal);
}

public static class AuditOutcomes
{
    public static (AuditOutcome Outcome, string? ErrorCode) Classify(Exception ex) => ex switch
    {
        AccessDeniedException or AuthorizationFailedException => (AuditOutcome.Denied, ex.GetType().Name),
        ErrorDetailException { ErrorCode: { } code } => (AuditOutcome.Failed, code),
        EntityNotFoundException => (AuditOutcome.Failed, "NotFound"),
        OperationCanceledException => (AuditOutcome.Failed, "Cancelled"),
        _ => (AuditOutcome.Failed, ex.GetType().Name)
    };
}

/// <summary>
/// How many audited commands run on this async flow. Not a static field of the generic decorator: that would be one per command type,
/// and a command sent by a command of another type would be logged on its own.
/// </summary>
internal static class AuditCommandDepth
{
    private static readonly AsyncLocal<int> depth = new();

    public static int Current
    {
        get => depth.Value;
        set => depth.Value = value;
    }
}
