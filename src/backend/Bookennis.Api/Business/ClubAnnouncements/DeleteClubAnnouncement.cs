using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

public record DeleteClubAnnouncement(int ClubId, int ClubAnnouncementId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteClubAnnouncement>
    {
        public async Task<Unit> Handle(DeleteClubAnnouncement request, CancellationToken cancellationToken)
        {
            var announcement = await context.ClubAnnouncements
                .SingleRequiredAsync(a => a.Id == request.ClubAnnouncementId && a.ClubId == request.ClubId, cancellationToken);

            context.ClubAnnouncements.Remove(announcement);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
