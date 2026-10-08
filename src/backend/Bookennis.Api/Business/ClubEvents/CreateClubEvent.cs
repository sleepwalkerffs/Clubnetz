using Bookennis.Api.Data;
using Bookennis.Domain.ClubEvents;

namespace Bookennis.Api.Business.ClubEvents;

public record CreateClubEvent(int ClubId, int UserId, ClubEvent.EventData Data) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateClubEvent, int>
    {
        public async Task<int> Handle(CreateClubEvent request, CancellationToken cancellationToken)
        {
            // Application administrators can manage events without being a member of the club
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            var clubEvent = new ClubEvent(request.ClubId, memberId, request.Data);
            context.ClubEvents.Add(clubEvent);

            await context.SaveChangesAsync(cancellationToken);

            return clubEvent.Id;
        }
    }
}
