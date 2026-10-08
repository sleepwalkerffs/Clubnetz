using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Booking;
using Bookennis.Shared.Controller.Booking.Shared;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record GetRecurringBookingSeries(int SeriesId) : IQuery<RecurringBookingSeriesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetRecurringBookingSeries, RecurringBookingSeriesResult>
    {
        public async Task<RecurringBookingSeriesResult> Handle(GetRecurringBookingSeries request, CancellationToken cancellationToken)
        {
            var series = await context.RecurringBookingSeries
                .Include(s => s.SeriesPlayers)
                .SingleRequiredAsync(s => s.Id == request.SeriesId, cancellationToken);

            var playerIds = series.SeriesPlayers.Select(p => p.MemberId).ToList();

            var players = await (
                from member in context.Set<Member>()
                join user in context.Users on member.UserId equals user.Id
                where playerIds.Contains(member.Id)
                select new PlayerResult(member.Id, user.FirstName, user.LastName, member.MemberType == MemberType.GuestMember, false)
            ).ToListAsync(cancellationToken);

            return new RecurringBookingSeriesResult
            {
                Id = series.Id,
                CourtId = series.CourtId,
                PlayModeId = series.PlayModeId,
                DayOfWeek = series.DayOfWeek,
                StartTime = series.StartTime,
                EndTime = series.EndTime,
                RecurrenceIntervalWeeks = series.RecurrenceIntervalWeeks,
                StartDate = series.StartDate,
                EndDate = series.EndDate,
                Comment = series.Comment,
                Players = players,
            };
        }
    }
}
