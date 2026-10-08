namespace Bookennis.Shared.Controller.Members;

public class GetMemberOverviewResult
{
    public required int TotalBookings { get; init; }
    public required double TotalHours { get; init; }
    public required DateTimeOffset? FirstPlayed { get; init; }
    public required DateTimeOffset? LastPlayed { get; init; }
    public required int UpcomingBookings { get; init; }

    /// <summary>All seasons of the club, newest first.</summary>
    public required List<MemberSeasonOverview> Seasons { get; init; }
}

public class MemberSeasonOverview
{
    public required int SeasonId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsEnrolled { get; init; }
    public required int Bookings { get; init; }
    public required double Hours { get; init; }
    public required int Badges { get; init; }
}
