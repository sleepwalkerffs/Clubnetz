using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Account;

public record LoginGuest(Guid GuestCode, int ClubId) : ICommand
{
    public class Handler(AppDbContext context, UserManager<User> userManager, SignInManager<User> signInManager) : IRequestHandler<LoginGuest>
    {
        public enum ErrorCode
        {
            GuestCardSetupIncorrectly = 0,
            GuestCardConsumed = 1
        }

        public async Task<Unit> Handle(LoginGuest request, CancellationToken cancellationToken)
        {
            var guestCard = await context.GuestCards.SingleRequiredAsync(x => x.Code == request.GuestCode, cancellationToken);
            var guestMember = await context.GuestMembers.SingleRequiredAsync(x => x.Id == guestCard.GuestMemberId, cancellationToken);

            if (guestMember.ClubId != request.ClubId)
                throw new PreconditionException(ErrorCode.GuestCardSetupIncorrectly, [request.GuestCode.ToString(), request.ClubId.ToString()], $"The code {request.GuestCode} does not match the club {request.ClubId}");

            var user = await context.Users.SingleRequiredAsync(guestMember.UserId, cancellationToken);
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }

            //check if there is still bookings available and then sign the use in
            var availableBookings = await context.GuestCards.Where(x => x.GuestMemberId == guestMember.Id).SumAsync(x => x.PurchasedBookings, cancellationToken);
            var consumedBookings = await context.BookingPlayers
                .Include(x => x.Booking) // important for query filter
                .Where(x => x.MemberId == guestMember.Id).CountAsync(cancellationToken);

            //here also check that if the last booking is not due he should be logged in as he maybe wants to delete or edit it
            if (availableBookings < consumedBookings)
                throw new PreconditionException(ErrorCode.GuestCardConsumed, "There are no more available bookings");

            if (availableBookings == consumedBookings)
            {
                var latestBooking = await context.BookingPlayers
                    .Include(x => x.Booking) // important for query filter
                    .Where(x => x.MemberId == guestMember.Id)
                    .OrderBy(x => x.Booking.Interval.From)
                    .Select(x => x.Booking)
                    .LastOrDefaultAsync(cancellationToken);

                if (latestBooking != null && DateTimeOffset.UtcNow > latestBooking.Interval.From)
                    throw new PreconditionException(ErrorCode.GuestCardConsumed, "There are no more available bookings");
            }

            var additionalClaims = new List<System.Security.Claims.Claim>
            {
                new(GuestSessionClaims.GuestSession, "true"),
                new(GuestSessionClaims.GuestMemberId, guestMember.Id.ToString())
            };

            await signInManager.SignInWithClaimsAsync(user, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTime.UtcNow.AddHours(1) }, additionalClaims);

            return default;
        }
    }
}
