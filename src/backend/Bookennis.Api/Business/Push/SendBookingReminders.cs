using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Notifications;
using Bookennis.Global;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push;

/// <summary>
/// Reminds the players of the bookings that start soon (see <see cref="PushSettings.BookingReminderLeadMinutes"/>)
/// by push notification and email, each according to the players' notification preferences.
/// Runs every few minutes as a recurring job for all clubs.
/// </summary>
public record SendBookingReminders : ICommand
{
    public class Handler(AppDbContext context, IPushNotificationService pushNotificationService, IMediator mediator, AppSettings appSettings) : IRequestHandler<SendBookingReminders>
    {
        public async Task<Unit> Handle(SendBookingReminders request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var leadTime = TimeSpan.FromMinutes(appSettings.Push.BookingReminderLeadMinutes);
            var until = now.Add(leadTime);

            var dueBookings = await (
                from booking in context.Bookings.IgnoreQueryFilters()
                join court in context.Courts.IgnoreQueryFilters() on booking.CourtId equals court.Id
                join club in context.Clubs.IgnoreQueryFilters() on booking.ClubId equals club.Id
                where booking.ReminderSentAt == null && booking.Interval.From > now && booking.Interval.From <= until
                orderby booking.Interval.From
                select new
                {
                    Booking = booking,
                    CourtName = court.Name,
                    ClubName = club.Name,
                    PlayerIds = booking.Players.Select(p => p.MemberId).ToList()
                }
            ).ToListAsync(cancellationToken);

            foreach (var due in dueBookings)
            {
                var booking = due.Booking;
                booking.MarkReminderSent();

                // Booked on short notice: the players just heard about it, a reminder right after would only be noise
                if (booking.Metadata.Created > booking.Interval.From.UtcDateTime - leadTime)
                    continue;

                var recipients = await PushRecipients.ForMembers(context, NotificationType.BookingReminder, due.PlayerIds, exceptUserId: null, cancellationToken);
                var localStart = booking.Interval.From.AddTimeZoneInfo(booking.TimeZoneInfoId);

                await pushNotificationService.Notify(
                    recipients,
                    culture => new PushNotification(
                        PushTexts.Get(culture, "BookingReminder_Title", PushTexts.Time(culture, TimeOnly.FromDateTime(localStart.DateTime))),
                        $"{due.CourtName} · {due.ClubName}",
                        $"/clubs/{booking.ClubId}/booking/{booking.Id}",
                        Tag: $"booking-{booking.Id}"),
                    cancellationToken);

                await SendEmails(booking.Id, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }

        private async Task SendEmails(int bookingId, CancellationToken cancellationToken)
        {
            var booking = await BookingEmailQueries.GetUpcomingBooking(context, bookingId, cancellationToken);
            if (booking is null)
                return;

            var recipients = await EmailRecipients.ForMembers(context, NotificationType.BookingReminder, booking.PlayerIds, exceptUserIds: null, cancellationToken);

            foreach (var recipient in recipients)
            {
                var member = new MemberVariables(recipient.FirstName, recipient.LastName);
                var variables = new BookingReminderEmailVariables(member, booking.ToVariables(recipient.Language.ToCultureInfo(), appSettings));

                await mediator.Send(
                    new SendClubEmail(booking.ClubId, ClubEmailType.BookingReminder, recipient.Email, member.FullName, recipient.Language, variables),
                    cancellationToken);
            }
        }
    }
}

/// <summary>Entry point of the recurring Hangfire job.</summary>
public class BookingReminderJob(IMediator mediator)
{
    public const string JobId = "booking-reminders";
    public const string Schedule = "*/5 * * * *";

    public Task Run() => mediator.Send(new SendBookingReminders());
}
