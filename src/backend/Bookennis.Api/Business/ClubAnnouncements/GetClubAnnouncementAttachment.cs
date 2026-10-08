using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

public record GetClubAnnouncementAttachmentResult(byte[] Data, string ContentType, string FileName);

/// <summary>The file of an attachment. Attachments of expired announcements are only available to members who manage the announcements.</summary>
public record GetClubAnnouncementAttachment(int ClubId, int ClubAnnouncementId, int AttachmentId, bool CanManage) : IQuery<GetClubAnnouncementAttachmentResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubAnnouncementAttachment, GetClubAnnouncementAttachmentResult>
    {
        public async Task<GetClubAnnouncementAttachmentResult> Handle(GetClubAnnouncementAttachment request, CancellationToken cancellationToken)
        {
            var today = ClubAnnouncementMapper.Today;

            var attachment = await context.ClubAnnouncementAttachments
                .Where(a => a.Id == request.AttachmentId
                            && a.ClubAnnouncementId == request.ClubAnnouncementId
                            && a.Announcement.ClubId == request.ClubId
                            && (request.CanManage || a.Announcement.ExpiresOn == null || a.Announcement.ExpiresOn >= today))
                .Select(a => new GetClubAnnouncementAttachmentResult(a.Content.Data, a.ContentType, a.FileName))
                .SingleOrDefaultAsync(cancellationToken);

            return attachment ?? throw new EntityNotFoundException(typeof(ClubAnnouncementAttachment));
        }
    }
}
