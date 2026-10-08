using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubAnnouncements;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

public record GetClubAnnouncement(int ClubId, int ClubAnnouncementId, bool CanManage) : IQuery<ClubAnnouncementDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<GetClubAnnouncement, ClubAnnouncementDto>
    {
        public async Task<ClubAnnouncementDto> Handle(GetClubAnnouncement request, CancellationToken cancellationToken)
        {
            var announcement = await context.ClubAnnouncements.AsNoTracking()
                .GetAnnouncementWithAttachments(request.ClubId, request.ClubAnnouncementId, cancellationToken);
            announcement.EnsureVisible(request.CanManage);

            return await ClubAnnouncementMapper.ToDto(context, renderer, announcement, request.CanManage, cancellationToken);
        }
    }
}
