using Bookennis.Domain.ClubAnnouncements;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

public static class ClubAnnouncementQueryExtensions
{
    /// <summary>The announcement with its attachments, but without their files.</summary>
    public static Task<ClubAnnouncement> GetAnnouncementWithAttachments(this IQueryable<ClubAnnouncement> announcements, int clubId, int clubAnnouncementId, CancellationToken cancellationToken)
        => announcements
            .Include(a => a.Attachments)
            .SingleRequiredAsync(a => a.Id == clubAnnouncementId && a.ClubId == clubId, cancellationToken);

    /// <summary>Expired announcements do not exist for members who can not manage them.</summary>
    public static void EnsureVisible(this ClubAnnouncement announcement, bool canManage)
    {
        if (!canManage && announcement.IsExpired(ClubAnnouncementMapper.Today))
            throw new EntityNotFoundException(typeof(ClubAnnouncement));
    }
}
