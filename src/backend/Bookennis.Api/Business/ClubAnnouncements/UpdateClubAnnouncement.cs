using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Api.Business.ClubAnnouncements;

public record UpdateClubAnnouncement(int ClubId, int ClubAnnouncementId, ClubAnnouncement.AnnouncementData Data) : ICommand<ClubAnnouncementDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<UpdateClubAnnouncement, ClubAnnouncementDto>
    {
        public async Task<ClubAnnouncementDto> Handle(UpdateClubAnnouncement request, CancellationToken cancellationToken)
        {
            var announcement = await context.ClubAnnouncements.GetAnnouncementWithAttachments(request.ClubId, request.ClubAnnouncementId, cancellationToken);

            announcement.Update(request.Data);

            await context.SaveChangesAsync(cancellationToken);

            return await ClubAnnouncementMapper.ToDto(context, renderer, announcement, canManage: true, cancellationToken);
        }
    }
}
