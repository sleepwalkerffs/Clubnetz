namespace Bookennis.Domain.ClubAnnouncements;

/// <summary>
/// The file of a <see cref="ClubAnnouncementAttachment"/>. Stored in its own table,
/// so loading an announcement with its attachments does not load the files.
/// </summary>
public class ClubAnnouncementAttachmentContent
{
    private ClubAnnouncementAttachmentContent() { }

    internal ClubAnnouncementAttachmentContent(byte[] data) => Data = data;

    public int ClubAnnouncementAttachmentId { get; private set; }
    public byte[] Data { get; private set; } = [];
}
