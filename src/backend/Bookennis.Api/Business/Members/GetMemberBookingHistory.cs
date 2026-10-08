using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

public record GetMemberBookingHistory(int MemberId, int SeasonId) : ICommand<GetMemberBookingHistoryResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMemberBookingHistory, GetMemberBookingHistoryResult>
    {
        public async Task<GetMemberBookingHistoryResult> Handle(GetMemberBookingHistory request, CancellationToken cancellationToken)
        {
            var currentClub = await context.Clubs.SingleRequiredAsync(cancellationToken);

            var season = await context.Seasons.SingleAsync(s => s.Id == request.SeasonId, cancellationToken);

            var fromUtc = DateTime.SpecifyKind(season.Period.From.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(season.Period.To.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            var userId = await context.Set<Member>()
                .Where(m => m.Id == request.MemberId)
                .Select(m => m.UserId)
                .SingleAsync(cancellationToken);

            var bookings = await (
                from booking in context.Bookings
                join playMode in context.PlayModes on booking.PlayModeId equals playMode.Id

                let users = (
                    from bp in context.BookingPlayers
                    join member in context.Set<Member>().IgnoreQueryFilters() on bp.MemberId equals member.Id
                    join user in context.Users on member.UserId equals user.Id
                    join club in context.Set<Club>().IgnoreQueryFilters() on member.ClubId equals club.Id
                    where bp.BookingEntryId == booking.Id
                    select new { member.Id, user.FirstName, user.LastName, member.MemberType, IsAtpPlayer = club.IsAtpClub && member.ClubId != currentClub.Id, UserId = user.Id, HasProfilePicture = context.UserProfilePictures.Any(p => p.UserId == user.Id) }
                ).ToList()

                where users.Any(u => u.UserId == userId)
                   && fromUtc <= booking.Interval.From && booking.Interval.From <= toUtc

                orderby booking.Interval.From descending
                select new { Booking = booking, Users = users, PlayMode = playMode }
            ).ToListAsync(cancellationToken);

            return new GetMemberBookingHistoryResult
            {
                Bookings = bookings.ConvertAll(booking =>
                    new BookingResult(
                        booking.Booking.Id,
                        booking.Users.Select(player => new PlayerResult(player.Id, player.FirstName, player.LastName, player.MemberType == MemberType.GuestMember, player.IsAtpPlayer, player.UserId, player.HasProfilePicture ? $"/api/Profile/picture/{player.UserId}" : null)).ToList(),
                        booking.Booking.CourtId,
                        booking.Booking.Interval,
                        new PlayModeDto
                        {
                            Id = booking.PlayMode.Id,
                            AllowedRoles = booking.PlayMode.AllowedRoles.Select(x => (Bookennis.Shared.Controller.Shared.MemberRole)x).ToList(),
                            FixedDuration = booking.PlayMode.FixedDuration,
                            FixedPlayerCount = booking.PlayMode.FixedPlayerCount,
                            IsChargingBookingSubscription = booking.PlayMode.IsChargingBookingSubscription,
                            Color = booking.PlayMode.Color.ToArgb(),
                            Name = booking.PlayMode.Name,
                            CanOverbook = booking.PlayMode.CanOverbook,
                            CommentAllowed = booking.PlayMode.CommentAllowed,
                            MaxBookingsPerSeason = booking.PlayMode.MaxBookingsPerSeason,
                            AllowRecurring = booking.PlayMode.AllowRecurring,
                        },
                        booking.Booking.Comment,
                        booking.Booking.RecurringBookingSeriesId,
                        booking.Booking.IsExcludedFromSeries
                    )
                )
            };
        }
    }
}
