using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary><c>SeasonSports</c>: sports per season id (empty = every sport); a season without an entry keeps its sports, a new one is for every sport.</summary>
public record UpdateMemberBookingOptions(int MemberId, int[] AllowedSeasonIds, int BookingsPerWeek, IReadOnlyDictionary<int, Sport[]>? SeasonSports = null) : ICommand
{
    public enum ErrorCode
    {
        MaxBookingsPerWeekExceeded = 0,
    }

    public class Handler(AppDbContext context) : AsyncRequestHandler<UpdateMemberBookingOptions>
    {
        protected override async Task Handle(UpdateMemberBookingOptions request, CancellationToken cancellationToken)
        {
            var queryResult = await (
                                        from member in context.ClubMembers
                                        join club in context.Clubs on member.ClubId equals club.Id
                                        where member.Id == request.MemberId
                                        select new { club, member }
                                    ).SingleRequiredAsync(cancellationToken);

            if (request.BookingsPerWeek > queryResult.club.ConcurrentAllowedBookings)
                throw new PreconditionException(ErrorCode.MaxBookingsPerWeekExceeded, "Bookings per week is limited by Club settings");

            queryResult.member.Update(request.BookingsPerWeek);

            var existingSeasonLinks = await context.MemberSeasons
                .Where(ms => ms.MemberId == request.MemberId)
                .ToListAsync(cancellationToken);

            // Only seasons of the member's club: ids of other clubs (or ones that don't exist) are ignored
            var clubId = queryResult.member.ClubId;
            var allowedSeasonIds = await context.Seasons
                .Where(s => s.ClubId == clubId && request.AllowedSeasonIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var toRemove = existingSeasonLinks.Where(ms => !allowedSeasonIds.Contains(ms.SeasonId)).ToList();
            var toAdd = allowedSeasonIds
                .Where(sid => existingSeasonLinks.All(ms => ms.SeasonId != sid))
                .Select(sid => new MemberSeason(request.MemberId, sid, sports: request.SeasonSports?.GetValueOrDefault(sid)))
                .ToList();

            foreach (var enrollment in existingSeasonLinks.Where(ms => allowedSeasonIds.Contains(ms.SeasonId)))
            {
                if (request.SeasonSports?.TryGetValue(enrollment.SeasonId, out var sports) == true)
                    enrollment.SetSports(sports);
            }

            context.MemberSeasons.RemoveRange(toRemove);
            context.MemberSeasons.AddRange(toAdd);

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}