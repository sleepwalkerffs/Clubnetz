using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Booking;
using Bookennis.Shared.Controller.Booking.Shared;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record GetUpcomingBookings(int? Amount, int UserId) : ICommand<GetUpcomingBookingsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetUpcomingBookings, GetUpcomingBookingsResult>
    {
        public async Task<GetUpcomingBookingsResult> Handle(GetUpcomingBookings request, CancellationToken cancellationToken)
        {
            var currentClub = await context.Clubs.SingleRequiredAsync(cancellationToken);

            var bookings = await (from booking in context.Bookings
                                  join playMode in context.PlayModes on booking.PlayModeId equals playMode.Id

                                  let users = (from bookingEntryPlayer in context.BookingPlayers
                                               join member in context.Set<Member>().IgnoreQueryFilters() on bookingEntryPlayer.MemberId equals member.Id
                                               join user in context.Users on member.UserId equals user.Id
                                               join club in context.Set<Club>().IgnoreQueryFilters() on member.ClubId equals club.Id

                                               where bookingEntryPlayer.BookingEntryId == booking.Id

                                               select new { MemberId = member.Id, user.FirstName, user.LastName, UserId = user.Id, member.MemberType, IsAtpPlayer = club.IsAtpClub && member.ClubId != currentClub.Id, HasProfilePicture = context.UserProfilePictures.Any(p => p.UserId == user.Id) }
                                              ).ToList()

                                  where users.Any(u => u.UserId == request.UserId)
                                     && booking.Interval.From > DateTimeOffset.UtcNow

                                  orderby booking.Interval.From
                                  select new { Booking = booking, Users = users, PlayMode = playMode }
                                 ).ToListAsync(cancellationToken);

            return new GetUpcomingBookingsResult(
                bookings.ConvertAll(
                    booking =>
                        new BookingResult(
                            booking.Booking.Id,
                            booking.Users.Select(player => new PlayerResult(player.MemberId, player.FirstName, player.LastName, player.MemberType == MemberType.GuestMember, player.IsAtpPlayer, player.UserId, player.HasProfilePicture ? $"/api/Profile/picture/{player.UserId}" : null)).ToList() ?? [],
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
            );
        }
    }
}