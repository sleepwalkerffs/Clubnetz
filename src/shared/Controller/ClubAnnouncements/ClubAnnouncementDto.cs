namespace Bookennis.Shared.Controller.ClubAnnouncements;

public record GetClubAnnouncementsResult
{
    public required List<ClubAnnouncementSummaryDto> Announcements { get; init; }
}

public record ClubAnnouncementSummaryDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }

    /// <summary>The beginning of the text without formatting.</summary>
    public required string Excerpt { get; init; }

    public required bool IsPinned { get; init; }
    public required DateTimeOffset PublishedAt { get; init; }
    public DateOnly? ExpiresOn { get; init; }

    /// <summary>Expired announcements are only returned to members who manage the announcements.</summary>
    public required bool IsExpired { get; init; }

    public required int AttachmentCount { get; init; }
    public string? CreatedByName { get; init; }
}

public record ClubAnnouncementDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }

    /// <summary>The Markdown source, used by the editor.</summary>
    public required string Body { get; init; }

    /// <summary>The body rendered and sanitized by the backend.</summary>
    public required string BodyHtml { get; init; }

    public required bool IsPinned { get; init; }
    public required DateTimeOffset PublishedAt { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public required bool IsExpired { get; init; }
    public string? CreatedByName { get; init; }
    public required List<ClubAnnouncementAttachmentDto> Attachments { get; init; }

    /// <summary>When the announcement was last sent as email. Only filled for members who manage the announcements.</summary>
    public DateTimeOffset? EmailSentAt { get; init; }

    public ClubAnnouncementAudience? EmailAudience { get; init; }
    public int? EmailRecipientCount { get; init; }
}

public record ClubAnnouncementAttachmentDto
{
    public required int Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }

    /// <summary>Size in bytes.</summary>
    public required int Size { get; init; }
}
