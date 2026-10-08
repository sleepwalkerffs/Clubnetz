using System.Globalization;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Notifications;
using Bookennis.Global;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push;

/// <summary>
/// Tells the players of a booking what happened to it. The user who made the change is not notified,
/// neither are bookings in the past.
/// </summary>
public interface IBookingPushNotifier
{
    Task BookingAdded(int bookingId, CancellationToken cancellationToken);

    Task RecurringBookingAdded(int seriesId, CancellationToken cancellationToken);

    /// <summary>Call before the booking is removed, the players are read from it.</summary>
    Task BookingDeleted(int bookingId, CancellationToken cancellationToken);

    /// <summary>
    /// For a series that is deleted from the given booking onward. Call before the bookings are removed.
    /// </summary>
    Task RecurringBookingDeleted(int firstDeletedBookingId, CancellationToken cancellationToken);
}

public class BookingPushNotifier(AppDbContext context, IPushNotificationService pushNotificationService, IUserAccessor userAccessor) : IBookingPushNotifier
{
    public async Task BookingAdded(int bookingId, CancellationToken cancellationToken)
    {
        var booking = await GetBooking(bookingId, cancellationToken);

        // The players of a series get one notification for the series instead of one per occurrence
        if (booking is null || booking.RecurringBookingSeriesId is not null)
            return;

        var (actorUserId, actorName) = await GetActor(cancellationToken);
        var recipients = await PushRecipients.ForMembers(context, NotificationType.BookingAdded, booking.PlayerIds, actorUserId, cancellationToken);

        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "BookingAdded_Title", PushTexts.DateAndTime(culture, booking.LocalStart)),
                Body(culture, booking.CourtName, actorName, "BookedBy"),
                $"/clubs/{booking.ClubId}/booking/{booking.Id}",
                Tag: $"booking-{booking.Id}"),
            cancellationToken);
    }

    public async Task RecurringBookingAdded(int seriesId, CancellationToken cancellationToken)
    {
        var series = await (
            from s in context.RecurringBookingSeries.IgnoreQueryFilters()
            join court in context.Courts.IgnoreQueryFilters() on s.CourtId equals court.Id
            where s.Id == seriesId
            select new
            {
                s.ClubId,
                s.DayOfWeek,
                s.StartTime,
                CourtName = court.Name,
                PlayerIds = s.SeriesPlayers.Select(p => p.MemberId).ToList()
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (series is null)
            return;

        var (actorUserId, actorName) = await GetActor(cancellationToken);
        var recipients = await PushRecipients.ForMembers(context, NotificationType.BookingAdded, series.PlayerIds, actorUserId, cancellationToken);

        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "RecurringBookingAdded_Title", culture.DateTimeFormat.GetDayName(series.DayOfWeek), PushTexts.Time(culture, series.StartTime)),
                Body(culture, series.CourtName, actorName, "BookedBy"),
                $"/clubs/{series.ClubId}/my-club",
                Tag: $"booking-series-{seriesId}"),
            cancellationToken);
    }

    public async Task BookingDeleted(int bookingId, CancellationToken cancellationToken)
    {
        var booking = await GetBooking(bookingId, cancellationToken);
        if (booking is null)
            return;

        var (actorUserId, actorName) = await GetActor(cancellationToken);
        var recipients = await PushRecipients.ForMembers(context, NotificationType.BookingDeleted, booking.PlayerIds, actorUserId, cancellationToken);

        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "BookingDeleted_Title", PushTexts.DateAndTime(culture, booking.LocalStart)),
                Body(culture, booking.CourtName, actorName, "CancelledBy"),
                $"/clubs/{booking.ClubId}/my-club",
                Tag: $"booking-{booking.Id}"),
            cancellationToken);
    }

    public async Task RecurringBookingDeleted(int firstDeletedBookingId, CancellationToken cancellationToken)
    {
        var booking = await GetBooking(firstDeletedBookingId, cancellationToken);
        if (booking is null)
            return;

        var (actorUserId, actorName) = await GetActor(cancellationToken);
        var recipients = await PushRecipients.ForMembers(context, NotificationType.BookingDeleted, booking.PlayerIds, actorUserId, cancellationToken);

        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "RecurringBookingDeleted_Title", PushTexts.Date(culture, DateOnly.FromDateTime(booking.LocalStart.DateTime))),
                Body(culture, booking.CourtName, actorName, "CancelledBy"),
                $"/clubs/{booking.ClubId}/my-club",
                Tag: $"booking-series-{booking.RecurringBookingSeriesId}"),
            cancellationToken);
    }

    private async Task<BookingInfo?> GetBooking(int bookingId, CancellationToken cancellationToken)
    {
        var booking = await (
            from b in context.Bookings.IgnoreQueryFilters()
            join court in context.Courts.IgnoreQueryFilters() on b.CourtId equals court.Id
            where b.Id == bookingId && b.Interval.From > DateTimeOffset.UtcNow
            select new
            {
                b.Id,
                b.ClubId,
                b.Interval.From,
                b.TimeZoneInfoId,
                b.RecurringBookingSeriesId,
                CourtName = court.Name,
                PlayerIds = b.Players.Select(p => p.MemberId).ToList()
            }
        ).SingleOrDefaultAsync(cancellationToken);

        return booking is null
            ? null
            : new BookingInfo(booking.Id, booking.ClubId, booking.From.AddTimeZoneInfo(booking.TimeZoneInfoId), booking.RecurringBookingSeriesId, booking.CourtName, booking.PlayerIds);
    }

    /// <summary>The signed-in user who made the change. Nobody if the change comes from a background job.</summary>
    private async Task<(int? UserId, string? Name)> GetActor(CancellationToken cancellationToken)
    {
        if (!userAccessor.TryGetUserId(out var userId))
            return (null, null);

        var name = await context.Users.Where(u => u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken);
        return (userId, name);
    }

    private static string Body(CultureInfo culture, string courtName, string? actorName, string actorKey)
        => actorName is null ? courtName : $"{courtName} · {PushTexts.Get(culture, actorKey, actorName)}";

    private sealed record BookingInfo(int Id, int ClubId, DateTimeOffset LocalStart, int? RecurringBookingSeriesId, string CourtName, List<int> PlayerIds);
}
