using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.ClubAnnouncements;

/// <summary>
/// Create and update model of an announcement.
/// Texts are validated by the domain, so empty texts result in a localized error instead of a generic bad request.
/// </summary>
public record SaveClubAnnouncementModel
{
    public string? Title { get; init; }

    /// <summary>Markdown.</summary>
    public string? Body { get; init; }

    public bool IsPinned { get; init; }

    /// <summary>Last day the announcement is shown to members.</summary>
    public DateOnly? ExpiresOn { get; init; }
}

public record PreviewClubAnnouncementModel
{
    public string? Body { get; init; }
}

public record PreviewClubAnnouncementResult
{
    public required string Html { get; init; }
}

public record SendClubAnnouncementEmailModel
{
    public ClubAnnouncementAudience Audience { get; init; }

    /// <summary>Only used with <see cref="ClubAnnouncementAudience.Roles"/>.</summary>
    public List<MemberRole> Roles { get; init; } = [];
}

public record ClubAnnouncementEmailRecipientsResult
{
    /// <summary>Number of different email addresses the announcement is (or would be) sent to.</summary>
    public required int RecipientCount { get; init; }
}

/// <summary>The limits of attachments, so the app can check files before uploading them.</summary>
public static class ClubAnnouncementLimits
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 10_000;
    public const int MaxAttachments = 5;
    public const int MaxAttachmentSizeInBytes = 5 * 1024 * 1024;
    public const int MaxTotalAttachmentSizeInBytes = 10 * 1024 * 1024;

    public static readonly string[] AllowedFileExtensions =
        [".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".txt", ".csv", ".ics"];
}
