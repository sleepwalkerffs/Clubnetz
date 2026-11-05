using System.Globalization;
using Bookennis.Shared.Controller.PlayRequests;

namespace Bookennis.Client.Pages.ClubShell.PlayRequests;

public static class PlayRequestHelpers
{
    /// <summary>The time in the time zone of the request (the club's), falling back to the device's.</summary>
    public static DateTimeOffset InZone(DateTimeOffset value, string timeZoneInfoId)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(timeZoneInfoId));
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return value.ToLocalTime();
        }
    }

    /// <summary>E.g. "Sat 3 Oct".</summary>
    public static string Day(PlayRequestDto request) => InZone(request.StartsAt, request.TimeZoneInfoId).ToString("ddd d MMM", CultureInfo.CurrentCulture);

    /// <summary>E.g. "18:00 – 19:30".</summary>
    public static string Times(PlayRequestDto request)
        => $"{InZone(request.StartsAt, request.TimeZoneInfoId):HH:mm} – {InZone(request.EndsAt, request.TimeZoneInfoId):HH:mm}";

    public static string DayAndTime(DateTimeOffset value, string timeZoneInfoId)
        => InZone(value, timeZoneInfoId).ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture);

    public static string FullName(string firstName, string lastName) => $"{firstName} {lastName}".Trim();

    /// <summary>Players who are in, the requester included.</summary>
    public static int Joined(PlayRequestDto request) => request.PlayersNeeded - request.OpenSpots;

    public static bool IsOpen(PlayRequestDto request) => request.State == PlayRequestState.Open;

    /// <summary>A request somebody else made that still waits for an answer of the caller.</summary>
    public static bool WaitsForMyAnswer(PlayRequestDto request)
        => !request.IsMine && IsOpen(request) && request.MyAnswer == PlayRequestAnswer.Invited;
}
