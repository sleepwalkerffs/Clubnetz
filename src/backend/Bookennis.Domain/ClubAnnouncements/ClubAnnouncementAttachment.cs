using Bookennis.Domain.Base;

namespace Bookennis.Domain.ClubAnnouncements;

/// <summary>A file attached to a <see cref="ClubAnnouncement"/>. Members download it in the app and it is attached to the announcement email.</summary>
public class ClubAnnouncementAttachment : DomainEntity
{
    /// <summary>
    /// The file types that can be attached, with the content type they are served as.
    /// The content type sent by the browser is never trusted.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedFileTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".ics"] = "text/calendar",
    };

#pragma warning disable CS8618
    private ClubAnnouncementAttachment() { }
#pragma warning restore CS8618

    internal ClubAnnouncementAttachment(ClubAnnouncement announcement, string fileName, string contentType, byte[] data)
    {
        Announcement = announcement;
        ClubAnnouncementId = announcement.Id;
        FileName = fileName;
        ContentType = contentType;
        Size = data.Length;
        Content = new ClubAnnouncementAttachmentContent(data);
    }

    public ClubAnnouncement Announcement { get; private set; }
    public int ClubAnnouncementId { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }

    /// <summary>Size of the file in bytes.</summary>
    public int Size { get; private set; }

    /// <summary>Only loaded when it is included explicitly.</summary>
    public ClubAnnouncementAttachmentContent Content { get; private set; }
}
