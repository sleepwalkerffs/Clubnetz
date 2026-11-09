namespace Bookennis.Shared.Controller.Admin.Audit;

/// <summary>The filters of the audit log explorer. Everything is optional; the newest entries come first.</summary>
public class AuditEventFilter
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <summary>UTC.</summary>
    public DateTime? From { get; set; }

    /// <summary>UTC, exclusive.</summary>
    public DateTime? To { get; set; }

    public int? ClubId { get; set; }

    /// <summary>Entries the user did, was the subject of, or did as impersonating administrator.</summary>
    public int? UserId { get; set; }

    public List<AuditEventKind>? Kinds { get; set; }
    public List<AuditOutcome>? Outcomes { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public bool ImpersonatedOnly { get; set; }
    public bool ApiKeyOnly { get; set; }

    /// <summary>Text in the summary, names, email, IP, action, path, error code or ids.</summary>
    public string? Search { get; set; }

    public Guid? CorrelationId { get; set; }

    /// <summary>The <see cref="AuditEventPage.NextCursor"/> of the page before.</summary>
    public string? Cursor { get; set; }

    public int PageSize { get; set; } = DefaultPageSize;
}

public record AuditEventListItem
{
    public required long Id { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required Guid CorrelationId { get; init; }
    public required AuditEventKind Kind { get; init; }
    public required string Action { get; init; }
    public required AuditOutcome Outcome { get; init; }
    public string? ErrorCode { get; init; }
    public int? ClubId { get; init; }
    public string? ClubName { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public int? SubjectUserId { get; init; }
    public string? SubjectName { get; init; }
    public int? ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public int? ImpersonatorUserId { get; init; }
    public string? ImpersonatorName { get; init; }
    public int? ApiKeyId { get; init; }
    public string? System { get; init; }
    public string? Request { get; init; }
    public string? Summary { get; init; }
}

public record AuditEventPage
{
    public required List<AuditEventListItem> Items { get; init; }

    /// <summary>Null when there are no older entries.</summary>
    public string? NextCursor { get; init; }
}

/// <summary>One field of a data change. Values are shown as text (JSON for anything that is not a string).</summary>
public record AuditFieldChange(string Field, string? Old, string? New, bool HasOld, bool HasNew);

public record AuditEventDetail
{
    public required AuditEventListItem Event { get; init; }
    public string? ActorEmail { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public int? DurationMs { get; init; }
    public required List<AuditFieldChange> Changes { get; init; }

    /// <summary>Everything else that happened in the same request or job (same correlation id), oldest first.</summary>
    public required List<AuditEventListItem> Related { get; init; }
}

public record AuditFilterOptions
{
    public required List<string> EntityTypes { get; init; }
    public required List<string> Actions { get; init; }
    public required List<AuditClubOption> Clubs { get; init; }
}

public record AuditClubOption(int Id, string Name);

public enum AuditBucketSize
{
    FiveMinutes,
    Hour,
    Day,
    Month
}

/// <param name="Start">Local time of the viewer (the offset they sent), without kind.</param>
public record AuditCountBucket(DateTime Start, int Succeeded, int Failed, int Denied)
{
    public int Total => Succeeded + Failed + Denied;
}

/// <summary>How many entries match the filter over time, for the chart above the list.</summary>
public record AuditEventCounts
{
    public required AuditBucketSize BucketSize { get; init; }
    public required List<AuditCountBucket> Buckets { get; init; }
}
