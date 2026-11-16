using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Notifications;

/// <summary>What the notifications show of a club event.</summary>
public sealed record ClubEventInfo(
    int Id,
    int ClubId,
    string ClubName,
    string Title,
    DateOnly StartDate,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? Location,
    DateTimeOffset? RegistrationDeadline,
    ClubRegion Region)
{
    /// <summary>
    /// Club events have club-local dates and times without a time zone (wall-clock times, see <see cref="ClubTimeZone"/>),
    /// so the time zone of the event's club is used to tell when an event starts and to show deadlines.
    /// </summary>
    public TimeZoneInfo TimeZone => Region.TimeZone;

    public DateTimeOffset? LocalRegistrationDeadline
        => RegistrationDeadline is { } deadline ? Region.ToLocalOffset(deadline) : null;

    public static async Task<ClubEventInfo?> Get(AppDbContext context, int clubEventId, int? clubId, CancellationToken cancellationToken)
    {
        var info = await (
            from e in context.ClubEvents.IgnoreQueryFilters()
            join club in context.Clubs.IgnoreQueryFilters() on e.ClubId equals club.Id
            where e.Id == clubEventId && (clubId == null || e.ClubId == clubId)
            select new { e.Id, e.ClubId, ClubName = club.Name, e.Title, e.StartDate, e.EndDate, e.StartTime, e.EndTime, e.Location, e.RegistrationDeadline, club.TimeZoneId, club.Country, club.Currency }
        ).SingleOrDefaultAsync(cancellationToken);

        return info is null
            ? null
            : new ClubEventInfo(
                info.Id, info.ClubId, info.ClubName, info.Title, info.StartDate, info.EndDate, info.StartTime, info.EndTime, info.Location, info.RegistrationDeadline,
                new ClubRegion(info.ClubId, info.TimeZoneId, info.Country, info.Currency));
    }

    /// <summary>Short date and start time for push notifications, e.g. "Sa 3. Oktober, 09:00" (pass the club's culture).</summary>
    public string PushDate(CultureInfo culture)
    {
        var when = PushTexts.Date(culture, StartDate);
        return StartTime is { } startTime ? $"{when}, {PushTexts.Time(culture, startTime)}" : when;
    }

    /// <summary>Pass the club's culture (<see cref="ClubRegion.Culture(Domain.User.Language)"/>).</summary>
    public EventVariables ToVariables(CultureInfo culture, AppSettings appSettings)
    {
        var date = NotificationTexts.Date(culture, StartDate);
        if (EndDate is { } endDate)
            date += $" – {NotificationTexts.Date(culture, endDate)}";

        var time = StartTime is { } startTime ? NotificationTexts.Time(culture, startTime) : null;
        if (time is not null && EndTime is { } endTime)
            time += $" – {NotificationTexts.Time(culture, endTime)}";

        var deadline = LocalRegistrationDeadline is { } localDeadline
            ? $"{NotificationTexts.Date(culture, DateOnly.FromDateTime(localDeadline.DateTime))}, {NotificationTexts.Time(culture, TimeOnly.FromDateTime(localDeadline.DateTime))}"
            : null;

        return new EventVariables(Title, date, time, Location, deadline, $"{appSettings.AppUri.AbsoluteUri.TrimEnd('/')}/clubs/{ClubId}/calendar/{Id}");
    }
}
