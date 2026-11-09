using Bookennis.Shared.Controller.Admin.Audit;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>
/// One entry of the audit log. Append-only: a trigger in the database refuses UPDATE and DELETE, except for the retention job and the
/// pseudonymization of deleted users (<see cref="AuditWriter"/>). Not a <c>DomainEntity</c> on purpose: no tenant filter (only application
/// administrators read it), no metadata, no domain events, and no foreign keys, so entries outlive the users and clubs they are about.
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }

    /// <summary>The same for everything that happened in one HTTP request or one background job: ties a command to its data changes.</summary>
    public Guid CorrelationId { get; set; }

    public AuditEventKind Kind { get; set; }

    /// <summary>Created / Modified / Deleted for data changes, otherwise the name of the command or query (e.g. <c>CancelBooking</c>) or the check (<c>AccessDenied</c>).</summary>
    public string Action { get; set; } = "";

    public AuditOutcome Outcome { get; set; }

    /// <summary>The error code of a failed command (<c>ErrorDetailException.ErrorCode</c>), the exception type otherwise, or the failed requirements of a denied request.</summary>
    public string? ErrorCode { get; set; }

    public int? ClubId { get; set; }

    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    /// <summary>The user the entry is about (the account that signed in, got a new password, was impersonated, a changed <c>User</c> row, ...).</summary>
    public int? SubjectUserId { get; set; }
    public string? SubjectName { get; set; }

    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }

    /// <summary>The application administrator who was signed in as <see cref="ActorUserId"/>.</summary>
    public int? ImpersonatorUserId { get; set; }
    public string? ImpersonatorName { get; set; }

    public int? ApiKeyId { get; set; }

    /// <summary>The background job (or "System") when nobody made a request.</summary>
    public string? System { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Method and path of the HTTP request, without the query string (it can hold tokens).</summary>
    public string? Request { get; set; }

    public string? Summary { get; set; }

    /// <summary>JSON: for data changes <c>{"Property": {"old": .., "new": ..}}</c>.</summary>
    public string? Changes { get; set; }

    public int? DurationMs { get; set; }
}
