using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;

namespace Bookennis.Api.Business.ClubAnnouncements;

public record CreateClubAnnouncement(int ClubId, int UserId, ClubAnnouncement.AnnouncementData Data) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateClubAnnouncement, int>
    {
        public async Task<int> Handle(CreateClubAnnouncement request, CancellationToken cancellationToken)
        {
            // Application administrators can write announcements without being a member of the club
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            var announcement = new ClubAnnouncement(request.ClubId, memberId, request.Data, DateTimeOffset.UtcNow);
            context.ClubAnnouncements.Add(announcement);

            await context.SaveChangesAsync(cancellationToken);

            return announcement.Id;
        }
    }
}
