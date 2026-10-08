using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

public record UpdateMemberBookingOptions(int MemberId, int[] AllowedSeasonIds, int BookingsPerWeek) : ICommand
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

            var toRemove = existingSeasonLinks.Where(ms => !request.AllowedSeasonIds.Contains(ms.SeasonId)).ToList();
            var toAdd = request.AllowedSeasonIds
                .Where(sid => existingSeasonLinks.All(ms => ms.SeasonId != sid))
                .Select(sid => new MemberSeason(request.MemberId, sid))
                .ToList();

            context.MemberSeasons.RemoveRange(toRemove);
            context.MemberSeasons.AddRange(toAdd);

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}