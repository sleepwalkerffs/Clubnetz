using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Base;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Notifications;
using Bookennis.Global;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Texts = Bookennis.Api.Resources.Localization.View_Emails_BookingDeleted;

namespace Bookennis.Api.Business.Shared.DomainEventHandlers;

public abstract class DeleteBookingsNotificationHandler<T>(
    AppDbContext context,
    IMediator mediator,
    IUserAccessor userAccessor,
    IBookingPushNotifier pushNotifier) : INotificationHandler<DomainEvent<T>> where T : IDomainEvent
{
    protected AppDbContext Context => context;

    public async Task Handle(DomainEvent<T> notification, CancellationToken cancellationToken)
    {
        var user = await context.Users.FindRequiredAsync(userAccessor.GetUserId(), cancellationToken);
        var intersectingBookings = await QueryBookingsToBeDeleted(notification, cancellationToken);

        foreach (var intersectingBooking in intersectingBookings)
        {
            var playerIds = await context.BookingPlayers
                .Where(p => p.Booking.Id == intersectingBooking.Booking.Id)
                .Select(p => p.MemberId)
                .ToListAsync(cancellationToken);
            var recipients = await EmailRecipients.ForMembers(context, NotificationType.BookingDeleted, playerIds, exceptUserIds: null, cancellationToken);

            foreach (var recipient in recipients)
            {
                var culture = recipient.Language.ToCultureInfo();
                var reasonTemplate = Texts.ResourceManager.GetString(intersectingBooking.ReasonKey, culture) ?? intersectingBooking.ReasonKey;
                var reason = intersectingBooking.ReasonFormatArg is not null
                    ? string.Format(reasonTemplate, intersectingBooking.ReasonFormatArg)
                    : reasonTemplate;

                var member = new MemberVariables(recipient.FirstName, recipient.LastName);
                var variables = new BookingDeletedEmailVariables(
                    member,
                    new BookingVariables(
                        intersectingBooking.Booking.Interval.From.AddTimeZoneInfo(intersectingBooking.Booking.TimeZoneInfoId).ToString(culture),
                        intersectingBooking.CourtName,
                        user.FullName,
                        reason));

                await mediator.Send(
                    new SendClubEmail(intersectingBooking.Booking.ClubId, ClubEmailType.BookingDeleted, recipient.Email, member.FullName, recipient.Language, variables),
                    cancellationToken);
            }

            await pushNotifier.BookingDeleted(intersectingBooking.Booking.Id, cancellationToken);
            context.Bookings.Remove(intersectingBooking.Booking);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    protected abstract Task<List<DeletedBookings>> QueryBookingsToBeDeleted(DomainEvent<T> notification, CancellationToken cancellationToken);

    public record DeletedBookings
    {
        public required Booking Booking { get; init; }
        public required string CourtName { get; init; }
        public required string ReasonKey { get; init; }
        public string? ReasonFormatArg { get; init; }
    }
}
