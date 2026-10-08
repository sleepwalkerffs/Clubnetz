using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
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
    DateTimeOffset? RegistrationDeadline)
{
    /// <summary>
    /// Club events have club-local dates and times without a time zone. The clubs are in Austria, Germany and
    /// Switzerland, which share one time zone, so it is used to tell when an event starts and to show deadlines.
    /// </summary>
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    public DateTimeOffset? LocalRegistrationDeadline
        => RegistrationDeadline is { } deadline ? TimeZoneInfo.ConvertTime(deadline, TimeZone) : null;

    public static async Task<ClubEventInfo?> Get(AppDbContext context, int clubEventId, int? clubId, CancellationToken cancellationToken)
        => await (
            from e in context.ClubEvents.IgnoreQueryFilters()
            join club in context.Clubs.IgnoreQueryFilters() on e.ClubId equals club.Id
            where e.Id == clubEventId && (clubId == null || e.ClubId == clubId)
            select new ClubEventInfo(e.Id, e.ClubId, club.Name, e.Title, e.StartDate, e.EndDate, e.StartTime, e.EndTime, e.Location, e.RegistrationDeadline)
        ).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Short date and start time for push notifications, e.g. "Sa 3. Oktober, 09:00".</summary>
    public string PushDate(CultureInfo culture)
    {
        var when = PushTexts.Date(culture, StartDate);
        return StartTime is { } startTime ? $"{when}, {PushTexts.Time(culture, startTime)}" : when;
    }

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
