using Bookennis.Api.Data;
using Bookennis.Domain.Courts;

namespace Bookennis.Api.Business.CourtBlockings;

/// <summary>
/// Blocks courts for a time window. Upcoming bookings in the blocked time are deleted and their players are notified
/// (see <see cref="DomainEventHandlers.DeleteBookingsOnCourtBlocking"/>), which has to be confirmed with <c>DeleteConflictingBookings</c>.
/// </summary>
public record CreateCourtBlocking(int ClubId, CourtBlocking.BlockingData Data, bool DeleteConflictingBookings) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateCourtBlocking, int>
    {
        public async Task<int> Handle(CreateCourtBlocking request, CancellationToken cancellationToken)
        {
            var blocking = new CourtBlocking(request.ClubId, request.Data);

            await context.EnsureCourtsBelongToClub(request.ClubId, request.Data.CourtIds, cancellationToken);
            await context.EnsureConflictsAreConfirmed(blocking, request.DeleteConflictingBookings, cancellationToken);

            context.CourtBlockings.Add(blocking);
            await context.SaveChangesAsync(cancellationToken);

            return blocking.Id;
        }
    }
}
