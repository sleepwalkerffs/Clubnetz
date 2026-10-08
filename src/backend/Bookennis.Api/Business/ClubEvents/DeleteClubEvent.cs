using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEvents;

public record DeleteClubEvent(int ClubId, int ClubEventId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteClubEvent>
    {
        public async Task<Unit> Handle(DeleteClubEvent request, CancellationToken cancellationToken)
        {
            var clubEvent = await context.ClubEvents
                .SingleRequiredAsync(e => e.Id == request.ClubEventId && e.ClubId == request.ClubId, cancellationToken);

            context.ClubEvents.Remove(clubEvent);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
