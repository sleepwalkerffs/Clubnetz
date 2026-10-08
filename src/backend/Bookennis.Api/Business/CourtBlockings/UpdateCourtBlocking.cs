using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Courts.Events;

namespace Bookennis.Api.Business.CourtBlockings;

/// <summary>Replaces all data of a court blocking. Like creating one, added courts or times delete the upcoming bookings in the blocked time.</summary>
public record UpdateCourtBlocking(int ClubId, int CourtBlockingId, CourtBlocking.BlockingData Data, bool DeleteConflictingBookings) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateCourtBlocking>
    {
        public async Task<Unit> Handle(UpdateCourtBlocking request, CancellationToken cancellationToken)
        {
            var blocking = await context.CourtBlockings.GetBlockingWithDetails(request.ClubId, request.CourtBlockingId, cancellationToken);

            blocking.Update(request.Data);

            await context.EnsureCourtsBelongToClub(request.ClubId, request.Data.CourtIds, cancellationToken);

            // Renaming a blocking or removing a court does not touch any bookings
            if (blocking.Events.OfType<CourtBlockedDomainEvent>().Any())
                await context.EnsureConflictsAreConfirmed(blocking, request.DeleteConflictingBookings, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
