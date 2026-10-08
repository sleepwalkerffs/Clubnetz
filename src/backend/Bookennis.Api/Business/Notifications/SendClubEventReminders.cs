using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Notifications;

/// <summary>
/// Reminds the registered members of the club events that start within <see cref="LeadTime"/>, and the members
/// who have not registered of registration deadlines that end within <see cref="LeadTime"/>.
/// Runs regularly as a recurring job for all clubs; every event is reminded of once.
/// </summary>
public record SendClubEventReminders : ICommand
{
    public static readonly TimeSpan LeadTime = TimeSpan.FromHours(24);

    /// <summary>When an all-day event is considered to start, so its reminder does not go out at midnight.</summary>
    public static readonly TimeOnly AllDayStart = new(8, 0);

    public class Handler(AppDbContext context, IClubEventNotifier notifier) : IRequestHandler<SendClubEventReminders>
    {
        public async Task<Unit> Handle(SendClubEventReminders request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var until = now.Add(LeadTime);

            await SendEventReminders(now, until, cancellationToken);
            await SendDeadlineReminders(now, until, cancellationToken);

            return default;
        }

        private async Task SendEventReminders(DateTimeOffset now, DateTimeOffset until, CancellationToken cancellationToken)
        {
            // The start is a club-local date and time, so the candidates are narrowed down by date and checked exactly below
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var candidates = await context.ClubEvents
                .IgnoreQueryFilters()
                .Where(e => e.ReminderSentAt == null && e.StartDate >= today.AddDays(-1) && e.StartDate <= today.AddDays(2))
                .OrderBy(e => e.StartDate)
                .ToListAsync(cancellationToken);

            foreach (var clubEvent in candidates)
            {
                var localStart = clubEvent.StartDate.ToDateTime(clubEvent.StartTime ?? AllDayStart);
                var start = new DateTimeOffset(localStart, ClubEventInfo.TimeZone.GetUtcOffset(localStart));
                if (start <= now || start > until)
                    continue;

                clubEvent.MarkReminderSent();

                // Planned on short notice: whoever registered did so just now, a reminder right after would only be noise
                if (clubEvent.Metadata.Created > start.UtcDateTime - LeadTime)
                    continue;

                await notifier.EventReminder(clubEvent.Id, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task SendDeadlineReminders(DateTimeOffset now, DateTimeOffset until, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var dueEvents = await context.ClubEvents
                .IgnoreQueryFilters()
                .Include(e => e.Registrations)
                .Where(e => e.DeadlineReminderSentAt == null
                            && e.RegistrationEnabled
                            && e.RegistrationDeadline > now
                            && e.RegistrationDeadline <= until
                            && (e.EndDate ?? e.StartDate) >= today)
                .OrderBy(e => e.RegistrationDeadline)
                .ToListAsync(cancellationToken);

            foreach (var clubEvent in dueEvents)
            {
                clubEvent.MarkDeadlineReminderSent();

                // Not for events the members were not told about, events nobody can register for anymore
                // and deadlines that were set on short notice
                if (!clubEvent.NotifyMembers || clubEvent.IsFull || clubEvent.Metadata.Created > clubEvent.RegistrationDeadline!.Value.UtcDateTime - LeadTime)
                    continue;

                await notifier.RegistrationDeadlineReminder(clubEvent.Id, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Entry point of the recurring Hangfire job.</summary>
public class ClubEventReminderJob(IMediator mediator)
{
    public const string JobId = "club-event-reminders";
    public const string Schedule = "*/15 * * * *";

    public Task Run() => mediator.Send(new SendClubEventReminders());
}
