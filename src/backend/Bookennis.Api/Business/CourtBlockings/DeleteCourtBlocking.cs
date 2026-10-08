using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtBlockings;

public record DeleteCourtBlocking(int ClubId, int CourtBlockingId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteCourtBlocking>
    {
        public async Task<Unit> Handle(DeleteCourtBlocking request, CancellationToken cancellationToken)
        {
            var blocking = await context.CourtBlockings
                .SingleRequiredAsync(b => b.Id == request.CourtBlockingId && b.ClubId == request.ClubId, cancellationToken);

            context.CourtBlockings.Remove(blocking);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
