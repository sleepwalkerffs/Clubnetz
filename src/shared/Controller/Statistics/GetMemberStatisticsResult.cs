namespace Bookennis.Shared.Controller.Statistics;

public class GetMemberStatisticsResult
{
    public required List<SeasonStatisticsResult> SeasonStatistics { get; init; }
    public required StatisticsData AllTime { get; init; }
}

public class SeasonStatisticsResult
{
    public required int SeasonId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsActivated { get; init; }
    public required StatisticsData? Data { get; init; }
    public required List<PlayModeQuotaEntry> PlayModeQuotas { get; init; }
    public required ClubComparison? ClubComparison { get; init; }
}

public class StatisticsData
{
    public required List<ChartEntry> CourtUsage { get; init; }
    public required List<PartnerEntry> PartnerUsage { get; init; }
    public required List<ChartEntry> PlayModeUsage { get; init; }
    public required double AverageOpponentAge { get; init; }
    public required int TotalBookings { get; init; }
    public required double TotalHoursOnCourt { get; init; }
    public required int DistinctPartners { get; init; }

    /// <summary>Bookings per weekday, index 0 = Monday … 6 = Sunday (local time of the booking).</summary>
    public required List<int> WeekdayCounts { get; init; }

    /// <summary>Bookings per start hour, index 0 … 23 (local time of the booking).</summary>
    public required List<int> HourCounts { get; init; }

    public required PlayerType? PlayerType { get; init; }
    public required int LongestWeekStreak { get; init; }
    public required int CurrentWeekStreak { get; init; }
    public required int? LongestBreakDays { get; init; }
    public required WeekRecord? BestWeek { get; init; }
    public required MonthRecord? BusiestMonth { get; init; }
    public required DateOnly? FirstBooking { get; init; }
    public required DateOnly? LastBooking { get; init; }
    public required List<DayActivity> ActivityDays { get; init; }
}

public enum PlayerType
{
    EarlyBird = 0,
    AfternoonAce = 1,
    NightOwl = 2,
    WeekendWarrior = 3,
    AllRounder = 4,
}

public class PartnerEntry
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string? ProfilePictureUrl { get; init; }
    public required int Count { get; init; }
}

public class WeekRecord
{
    public required DateOnly WeekStart { get; init; }
    public required int Count { get; init; }
}

public class MonthRecord
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required int Count { get; init; }
}

public class DayActivity
{
    public required DateOnly Date { get; init; }
    public required int Count { get; init; }
}

public class ClubComparison
{
    /// <summary>Average bookings of all club members enrolled in the season (including the member).</summary>
    public required double ClubAverageBookings { get; init; }

    /// <summary>Share (0–100) of the other enrolled members who played fewer bookings than the member.</summary>
    public required int BetterThanPercentage { get; init; }

    public required int EnrolledMembers { get; init; }
}

public class ChartEntry
{
    public required string Label { get; init; }
    public required double Value { get; init; }
}

public class PlayModeQuotaEntry
{
    public required string PlayModeName { get; init; }
    public required int MaxBookingsPerSeason { get; init; }
    public required int UsedBookings { get; init; }
}
