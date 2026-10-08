using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Account;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Shared;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Guests;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global;
using Bookennis.Shared.Controller.Guests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Guests;

public record CreateGuestCard(int PurchasedBookings, [EmailAddress] string Email, string FirstName, string LastName, DateOnly Birthday, Gender Gender) : ICommand<CreateGuestCardResult>
{
    public class Handler(
        AppDbContext context,
        UserManager<User> userManager,
        ITenantService tenantService,
        AppSettings appSettings,
        IMediator mediator) : IRequestHandler<CreateGuestCard, CreateGuestCardResult>
    {
        public enum ErrorCode
        {
            NoClubId = 0,
            BookingsAvailable = 1
        }

        public async Task<CreateGuestCardResult> Handle(CreateGuestCard request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByEmailAsync(request.Email.Clean());
            if (user == null)
            {
                user = new User(request.Email, request.Email, request.FirstName, request.LastName, request.Birthday, request.Gender);
                var result = await userManager.CreateAsync(user);
                await userManager.AddToRoleAsync(user, UserRoles.User.ToString());
                if (!result.Succeeded)
                {
                    throw new PreconditionException(AccountErrorCode.IdentityError, result.Errors.Select(x => x.Code).ToArray(), "Error when changing the password.");
                }
            }

            if (!tenantService.TryGetTenantId(out var clubId))
                throw new PreconditionException(ErrorCode.NoClubId, "Error getting the club Id try to sign out and try again");

            var guestMember = context.GuestMembers.SingleOrDefault(x => x.UserId == user.Id && x.ClubId == clubId);
            if (guestMember == null)
            {
                guestMember = new GuestMember(user.Id, clubId);
                await context.GuestMembers.AddAsync(guestMember, cancellationToken);
            }

            var availableBookings = await context.GuestCards.Where(x => x.GuestMemberId == guestMember.Id).SumAsync(x => x.PurchasedBookings, cancellationToken);
            var consumedBookings = await context.BookingPlayers
                .Include(x => x.Booking) // important for query filter
                .Where(x => x.MemberId == guestMember.Id).CountAsync(cancellationToken);

            guestMember.AllowBooking();
            var guestCard = new GuestCard(clubId, Guid.NewGuid(), request.PurchasedBookings, guestMember.Id);

            await context.GuestCards.AddAsync(guestCard, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var url = appSettings.AppUri.AbsoluteUri.TrimEnd('/') + "/Account/GuestLogin";
            var newUrl = new Uri(QueryHelpers.AddQueryString(url, new Dictionary<string, string?>() { { "guestCode", guestCard.Code.ToString() }, { "clubId", guestCard.ClubId.ToString() } }));
            var member = new MemberVariables(user.FirstName, user.LastName);
            await mediator.Send(
                new SendClubEmail(clubId, ClubEmailType.GuestCard, request.Email, member.FullName, user.Language, new GuestCardEmailVariables(member, newUrl.AbsoluteUri)),
                cancellationToken);
            return new CreateGuestCardResult { GuestCardId = guestCard.Id };
        }
    }
}
