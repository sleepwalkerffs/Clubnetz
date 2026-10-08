using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.CourtBlockings;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtBlockings;

/// <summary>The upcoming bookings a (not yet saved) court blocking would delete. Shown to the user before the blocking is saved.</summary>
public record GetCourtBlockingConflicts(int ClubId, CourtBlocking.BlockingData Data) : IQuery<GetCourtBlockingConflictsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetCourtBlockingConflicts, GetCourtBlockingConflictsResult>
    {
        public async Task<GetCourtBlockingConflictsResult> Handle(GetCourtBlockingConflicts request, CancellationToken cancellationToken)
        {
            // Not added to the context, it only calculates the blocked time windows
            var blocking = new CourtBlocking(request.ClubId, request.Data);

            var conflicts = await context.GetConflictingBookings(blocking, DateTimeOffset.UtcNow, cancellationToken);
            var bookingIds = conflicts.ConvertAll(b => b.Id);
            if (bookingIds.Count == 0)
                return new GetCourtBlockingConflictsResult { Bookings = [] };

            var bookings = await (
                from booking in context.Bookings
                join court in context.Courts on booking.CourtId equals court.Id

                // Players of other clubs can take part in a booking (ATP)
                let players = (
                    from bookingPlayer in context.BookingPlayers
                    join member in context.Set<Member>().IgnoreQueryFilters() on bookingPlayer.MemberId equals member.Id
                    join user in context.Users on member.UserId equals user.Id
                    where bookingPlayer.BookingEntryId == booking.Id
                    orderby user.FirstName, user.LastName
                    select user.FirstName + " " + user.LastName
                ).ToList()

                where bookingIds.Contains(booking.Id)
                orderby booking.Interval.From, court.SortOrder
                select new CourtBlockingConflictDto
                {
                    BookingId = booking.Id,
                    CourtName = court.Name,
                    Interval = booking.Interval,
                    Players = players,
                }
            )
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return new GetCourtBlockingConflictsResult { Bookings = bookings };
        }
    }
}
