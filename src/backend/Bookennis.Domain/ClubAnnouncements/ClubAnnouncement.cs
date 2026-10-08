using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.ClubAnnouncements;

/// <summary>
/// News of a club for its members: a title and a Markdown body, optionally pinned to the top, with an
/// expiry date and attached files. It can additionally be sent to a group of members as email.
/// </summary>
public class ClubAnnouncement : TenantDomainEntity, IAggregateRoot
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 10_000;
    public const int MaxFileNameLength = 150;
    public const int MaxAttachments = 5;
    public const int MaxAttachmentSizeInBytes = 5 * 1024 * 1024;

    /// <summary>All attachments together. Mail servers commonly reject emails larger than 20-25 MB, and attachments grow by a third when encoded.</summary>
    public const int MaxTotalAttachmentSizeInBytes = 10 * 1024 * 1024;

    public enum ErrorCode
    {
        ClubAnnouncementTitleRequired = 0,
        ClubAnnouncementBodyRequired = 1,
        ClubAnnouncementTextTooLong = 2,
        ClubAnnouncementTooManyAttachments = 3,
        ClubAnnouncementAttachmentTooLarge = 4,
        ClubAnnouncementAttachmentsTooLarge = 5,
        ClubAnnouncementAttachmentTypeNotAllowed = 6,
        ClubAnnouncementAttachmentEmpty = 7,
    }

    public record AnnouncementData(string Title, string Body, bool IsPinned, DateOnly? ExpiresOn);

    private readonly List<ClubAnnouncementAttachment> attachments = new();

#pragma warning disable CS8618
    private ClubAnnouncement() { }
#pragma warning restore CS8618

    public ClubAnnouncement(int clubId, int? createdByMemberId, AnnouncementData data, DateTimeOffset now)
    {
        ClubId = clubId;
        CreatedByMemberId = createdByMemberId;
        PublishedAt = now.ToUniversalTime();
        Update(data);
    }

    public int? CreatedByMemberId { get; private set; }
    public string Title { get; private set; } = "";

    /// <summary>Markdown.</summary>
    public string Body { get; private set; } = "";

    /// <summary>Pinned announcements are listed before all others.</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Last day the announcement is shown to members; <c>null</c> if it does not expire.</summary>
    public DateOnly? ExpiresOn { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>When the announcement was last sent as email; <c>null</c> if it was never sent.</summary>
    public DateTimeOffset? EmailSentAt { get; private set; }

    public ClubAnnouncementAudience? EmailAudience { get; private set; }
    public int? EmailRecipientCount { get; private set; }

    public IReadOnlyList<ClubAnnouncementAttachment> Attachments => attachments.AsReadOnly();

    public bool IsExpired(DateOnly today) => ExpiresOn < today;

    public void Update(AnnouncementData data)
    {
        var title = data.Title?.Trim() ?? "";
        if (title.Length == 0)
            throw new PreconditionException(ErrorCode.ClubAnnouncementTitleRequired, "A title is required.");

        var body = data.Body?.Trim() ?? "";
        if (body.Length == 0)
            throw new PreconditionException(ErrorCode.ClubAnnouncementBodyRequired, "A text is required.");

        if (title.Length > MaxTitleLength || body.Length > MaxBodyLength)
            throw new PreconditionException(ErrorCode.ClubAnnouncementTextTooLong, "A text is too long.");

        Title = title;
        Body = body;
        IsPinned = data.IsPinned;
        ExpiresOn = data.ExpiresOn;
    }

    public ClubAnnouncementAttachment AddAttachment(string fileName, byte[] data)
    {
        fileName = NormalizeFileName(fileName);

        if (!ClubAnnouncementAttachment.AllowedFileTypes.TryGetValue(Path.GetExtension(fileName), out var contentType))
            throw new PreconditionException(ErrorCode.ClubAnnouncementAttachmentTypeNotAllowed, "This file type can not be attached.");

        if (data.Length == 0)
            throw new PreconditionException(ErrorCode.ClubAnnouncementAttachmentEmpty, "The file is empty.");

        if (attachments.Count >= MaxAttachments)
            throw new PreconditionException(ErrorCode.ClubAnnouncementTooManyAttachments, [MaxAttachments.ToString()], $"An announcement can have at most {MaxAttachments} attachments.");

        if (data.Length > MaxAttachmentSizeInBytes)
            throw new PreconditionException(ErrorCode.ClubAnnouncementAttachmentTooLarge, [ToMegabytes(MaxAttachmentSizeInBytes)], "The file is too large.");

        if (attachments.Sum(a => (long)a.Size) + data.Length > MaxTotalAttachmentSizeInBytes)
            throw new PreconditionException(ErrorCode.ClubAnnouncementAttachmentsTooLarge, [ToMegabytes(MaxTotalAttachmentSizeInBytes)], "The attachments are too large in total.");

        var attachment = new ClubAnnouncementAttachment(this, fileName, contentType, data);
        attachments.Add(attachment);
        return attachment;
    }

    /// <summary>Returns <c>false</c> if the announcement has no such attachment.</summary>
    public bool RemoveAttachment(int attachmentId) => attachments.RemoveAll(a => a.Id == attachmentId) > 0;

    public void MarkEmailSent(ClubAnnouncementAudience audience, int recipientCount, DateTimeOffset now)
    {
        EmailSentAt = now.ToUniversalTime();
        EmailAudience = audience;
        EmailRecipientCount = recipientCount;
    }

    /// <summary>Removes any path and control characters and shortens the name (keeping the extension).</summary>
    private static string NormalizeFileName(string? fileName)
    {
        var name = (fileName ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();

        var extension = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name).Trim();
        if (baseName.Length == 0)
            baseName = "attachment";

        var maxBaseLength = Math.Max(1, MaxFileNameLength - extension.Length);
        if (baseName.Length > maxBaseLength)
            baseName = baseName[..maxBaseLength];

        return baseName + extension;
    }

    private static string ToMegabytes(int bytes) => (bytes / (1024 * 1024)).ToString();
}
