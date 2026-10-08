using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Booking.Shared;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record GetBooking(int Id) : IQuery<BookingResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBooking, BookingResult>
    {
        public async Task<BookingResult> Handle(GetBooking request, CancellationToken cancellationToken)
        {
            var currentClub = await context.Clubs.SingleRequiredAsync(cancellationToken);

            var booking = await (
               from bookingEntry in context.Bookings
               join playMode in context.PlayModes on bookingEntry.PlayModeId equals playMode.Id

               let users = (from bookingEntryPlayer in context.BookingPlayers
                            join member in context.Set<Member>().IgnoreQueryFilters() on bookingEntryPlayer.MemberId equals member.Id
                            join user in context.Users on member.UserId equals user.Id
                            join club in context.Set<Club>().IgnoreQueryFilters() on member.ClubId equals club.Id
                            where bookingEntryPlayer.BookingEntryId == bookingEntry.Id
                            select new { member.Id, user.FirstName, user.LastName, UserId = user.Id, member.MemberType, IsAtpPlayer = club.IsAtpClub && member.ClubId != currentClub.Id, HasProfilePicture = context.UserProfilePictures.Any(p => p.UserId == user.Id) }
                           ).ToList()

               where bookingEntry.Id == request.Id
               select new { Booking = bookingEntry, Users = users, PlayMode = playMode }
           ).SingleRequiredAsync(cancellationToken);

            var allMemberIds = booking.Users.Select(u => u.Id).ToList();

            var displayBadgeByMember = await (
                from settings in context.MemberBadgeSettings
                join mb in context.MemberBadges on settings.DisplayBadgeId equals mb.Id
                join bt in context.BadgeTiers on mb.BadgeTierId equals bt.Id
                where allMemberIds.Contains(settings.MemberId)
                select new { settings.MemberId, TierId = bt.Id, bt.ClubId, bt.Level, HasImage = context.BadgeTierImages.Any(i => i.BadgeTierId == bt.Id) }
            ).ToDictionaryAsync(x => x.MemberId, cancellationToken);

            return
                new BookingResult(
                    booking.Booking.Id,
                    booking.Users.Select(player =>
                    {
                        displayBadgeByMember.TryGetValue(player.Id, out var badge);
                        var badgeImageUrl = badge is { HasImage: true } ? $"/api/Clubs/{badge.ClubId}/BadgeTiers/{badge.TierId}/image" : null;
                        return new PlayerResult(player.Id, player.FirstName, player.LastName, player.MemberType == MemberType.GuestMember, player.IsAtpPlayer, player.UserId, player.HasProfilePicture ? $"/api/Profile/picture/{player.UserId}" : null, badgeImageUrl, badge?.Level);
                    }).ToList() ?? [],
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
                        AllowRecurring = booking.PlayMode.AllowRecurring
                    },
                    booking.Booking.Comment,
                    booking.Booking.RecurringBookingSeriesId,
                    booking.Booking.IsExcludedFromSeries
                );
        }
    }
}
