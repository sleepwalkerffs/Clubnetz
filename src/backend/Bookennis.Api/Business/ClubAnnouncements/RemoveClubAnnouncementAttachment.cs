using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>Removes an attachment. Nothing happens if the announcement has no such attachment.</summary>
public record RemoveClubAnnouncementAttachment(int ClubId, int ClubAnnouncementId, int AttachmentId) : ICommand<ClubAnnouncementDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<RemoveClubAnnouncementAttachment, ClubAnnouncementDto>
    {
        public async Task<ClubAnnouncementDto> Handle(RemoveClubAnnouncementAttachment request, CancellationToken cancellationToken)
        {
            var announcement = await context.ClubAnnouncements.GetAnnouncementWithAttachments(request.ClubId, request.ClubAnnouncementId, cancellationToken);

            if (announcement.RemoveAttachment(request.AttachmentId))
                await context.SaveChangesAsync(cancellationToken);

            return await ClubAnnouncementMapper.ToDto(context, renderer, announcement, canManage: true, cancellationToken);
        }
    }
}
