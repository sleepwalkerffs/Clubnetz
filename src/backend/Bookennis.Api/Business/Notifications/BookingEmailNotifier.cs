using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members;
using Bookennis.Domain.Notifications;
using Bookennis.Global;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Notifications;

/// <summary>
/// Tells the players of a booking by email what happened to it (the email counterpart of
/// <see cref="Push.IBookingPushNotifier"/>). The user who made the change gets no email, neither do
/// bookings in the past.
/// </summary>
public interface IBookingEmailNotifier
{
    Task BookingAdded(int bookingId, CancellationToken cancellationToken);

    Task RecurringBookingAdded(int seriesId, CancellationToken cancellationToken);

    /// <summary>A player cancelled the booking. Call before the booking is removed, the players are read from it.</summary>
    Task BookingDeleted(int bookingId, CancellationToken cancellationToken);

    /// <summary>
    /// For a series that is deleted from the given booking onward. Call before the bookings are removed.
    /// </summary>
    Task RecurringBookingDeleted(int firstDeletedBookingId, CancellationToken cancellationToken);
}

public class BookingEmailNotifier(AppDbContext context, IMediator mediator, IUserAccessor userAccessor, AppSettings appSettings) : IBookingEmailNotifier
{
    public async Task BookingAdded(int bookingId, CancellationToken cancellationToken)
    {
        var booking = await BookingEmailQueries.GetUpcomingBooking(context, bookingId, cancellationToken);

        // The players of a series get one email for the series instead of one per occurrence
        if (booking is null || booking.RecurringBookingSeriesId is not null)
            return;

        var (actorUserId, actorName) = await GetActor(cancellationToken);
        var recipients = await EmailRecipients.ForMembers(context, NotificationType.BookingAdded, booking.PlayerIds, Except(actorUserId), cancellationToken);

        foreach (var recipient in recipients)
        {
            var culture = recipient.Language.ToCultureInfo();
            var member = new MemberVariables(recipient.FirstName, recipient.LastName);
            var variables = new BookingAddedEmailVariables(
                member,
                booking.ToVariables(culture, appSettings),
                actorName ?? NotificationTexts.Get(culture, "Someone"));

            await mediator.Send(
                new SendClubEmail(booking.ClubId, ClubEmailType.BookingAdded, recipient.Email, member.FullName, recipient.Language, variables),
                cancellationToken);
        }
    }

    public async Task RecurringBookingAdded(int seriesId, CancellationToken cancellationToken)
    {
        var series = await (
            from s in context.RecurringBookingSeries.IgnoreQueryFilters()
            join court in context.Courts.IgnoreQueryFilters() on s.CourtId equals court.Id
            join playMode in context.PlayModes.IgnoreQueryFilters() on s.PlayModeId equals playMode.Id
            where s.Id == seriesId
            select new
            {
                s.ClubId,
                s.DayOfWeek,
                s.StartTime,
                s.EndTime,
                s.StartDate,
                s.RecurrenceIntervalWeeks,
                CourtName = court.Name,
                PlayModeName = playMode.Name,
                PlayerIds = s.SeriesPlayers.Select(p => p.MemberId).ToList()
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (series is null)
            return;

        var (actorUserId, actorName) = await GetActor(cancellationToken);
        var recipients = await EmailRecipients.ForMembers(context, NotificationType.BookingAdded, series.PlayerIds, Except(actorUserId), cancellationToken);
        if (recipients.Count == 0)
            return;

        var players = await BookingEmailQueries.GetPlayerNames(context, series.PlayerIds, cancellationToken);
        var appUri = appSettings.AppUri.AbsoluteUri.TrimEnd('/');

        foreach (var recipient in recipients)
        {
            var culture = recipient.Language.ToCultureInfo();
            var member = new MemberVariables(recipient.FirstName, recipient.LastName);
            var dayName = culture.DateTimeFormat.GetDayName(series.DayOfWeek);
            var startDate = NotificationTexts.Date(culture, series.StartDate);

            var variables = new BookingAddedEmailVariables(
                member,
                new BookingDetailsVariables(
                    series.RecurrenceIntervalWeeks == 1
                        ? NotificationTexts.Get(culture, "Recurring_Weekly", dayName, startDate)
                        : NotificationTexts.Get(culture, "Recurring_EveryNWeeks", dayName, startDate, series.RecurrenceIntervalWeeks),
                    $"{NotificationTexts.Time(culture, series.StartTime)} – {NotificationTexts.Time(culture, series.EndTime)}",
                    series.CourtName,
                    series.PlayModeName,
                    players,
                    $"{appUri}/clubs/{series.ClubId}/my-club"),
                actorName ?? NotificationTexts.Get(culture, "Someone"));

            await mediator.Send(
                new SendClubEmail(series.ClubId, ClubEmailType.BookingAdded, recipient.Email, member.FullName, recipient.Language, variables),
                cancellationToken);
        }
    }

    public Task BookingDeleted(int bookingId, CancellationToken cancellationToken)
        => SendDeleted(bookingId, "ReasonCancelled", cancellationToken);

    public Task RecurringBookingDeleted(int firstDeletedBookingId, CancellationToken cancellationToken)
        => SendDeleted(firstDeletedBookingId, "ReasonSeriesCancelled", cancellationToken);

    private async Task SendDeleted(int bookingId, string reasonKey, CancellationToken cancellationToken)
    {
        var booking = await BookingEmailQueries.GetUpcomingBooking(context, bookingId, cancellationToken);
        if (booking is null)
            return;

        var (actorUserId, actorName) = await GetActor(cancellationToken);
        var recipients = await EmailRecipients.ForMembers(context, NotificationType.BookingDeleted, booking.PlayerIds, Except(actorUserId), cancellationToken);

        foreach (var recipient in recipients)
        {
            var culture = recipient.Language.ToCultureInfo();
            var member = new MemberVariables(recipient.FirstName, recipient.LastName);
            var variables = new BookingDeletedEmailVariables(
                member,
                new BookingVariables(
                    booking.LocalStart.ToString(culture),
                    booking.CourtName,
                    actorName ?? NotificationTexts.Get(culture, "Someone"),
                    NotificationTexts.Get(culture, reasonKey)));

            await mediator.Send(
                new SendClubEmail(booking.ClubId, ClubEmailType.BookingDeleted, recipient.Email, member.FullName, recipient.Language, variables),
                cancellationToken);
        }
    }

    /// <summary>The signed-in user who made the change. Nobody if the change comes from a background job.</summary>
    private async Task<(int? UserId, string? Name)> GetActor(CancellationToken cancellationToken)
    {
        if (!userAccessor.TryGetUserId(out var userId))
            return (null, null);

        var name = await context.Users.Where(u => u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken);
        return (userId, name);
    }

    private static int[]? Except(int? userId) => userId is null ? null : [userId.Value];
}

/// <summary>What the booking emails show of a booking.</summary>
public sealed record BookingEmailInfo(
    int Id,
    int ClubId,
    DateTimeOffset LocalStart,
    DateTimeOffset LocalEnd,
    int? RecurringBookingSeriesId,
    string CourtName,
    string PlayModeName,
    string Players,
    List<int> PlayerIds)
{
    public BookingDetailsVariables ToVariables(CultureInfo culture, AppSettings appSettings)
        => new(
            NotificationTexts.Date(culture, DateOnly.FromDateTime(LocalStart.DateTime)),
            $"{NotificationTexts.Time(culture, TimeOnly.FromDateTime(LocalStart.DateTime))} – {NotificationTexts.Time(culture, TimeOnly.FromDateTime(LocalEnd.DateTime))}",
            CourtName,
            PlayModeName,
            Players,
            $"{appSettings.AppUri.AbsoluteUri.TrimEnd('/')}/clubs/{ClubId}/booking/{Id}");
}

public static class BookingEmailQueries
{
    /// <summary>The booking with what the emails show of it, or nothing if it already started.</summary>
    public static async Task<BookingEmailInfo?> GetUpcomingBooking(AppDbContext context, int bookingId, CancellationToken cancellationToken)
    {
        var booking = await (
            from b in context.Bookings.IgnoreQueryFilters()
            join court in context.Courts.IgnoreQueryFilters() on b.CourtId equals court.Id
            join playMode in context.PlayModes.IgnoreQueryFilters() on b.PlayModeId equals playMode.Id
            where b.Id == bookingId && b.Interval.From > DateTimeOffset.UtcNow
            select new
            {
                b.Id,
                b.ClubId,
                b.Interval.From,
                b.Interval.To,
                b.TimeZoneInfoId,
                b.RecurringBookingSeriesId,
                CourtName = court.Name,
                PlayModeName = playMode.Name,
                PlayerIds = b.Players.Select(p => p.MemberId).ToList()
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (booking is null)
            return null;

        return new BookingEmailInfo(
            booking.Id,
            booking.ClubId,
            booking.From.AddTimeZoneInfo(booking.TimeZoneInfoId),
            booking.To.AddTimeZoneInfo(booking.TimeZoneInfoId),
            booking.RecurringBookingSeriesId,
            booking.CourtName,
            booking.PlayModeName,
            await GetPlayerNames(context, booking.PlayerIds, cancellationToken),
            booking.PlayerIds);
    }

    /// <summary>The names of the players, separated by commas.</summary>
    public static async Task<string> GetPlayerNames(AppDbContext context, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken)
    {
        var names = await (
            from member in context.Set<Member>().IgnoreQueryFilters()
            join user in context.Users on member.UserId equals user.Id
            where memberIds.Contains(member.Id)
            orderby user.LastName, user.FirstName
            select user.FullName
        ).ToListAsync(cancellationToken);

        return string.Join(", ", names);
    }
}
