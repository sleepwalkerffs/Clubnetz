namespace Bookennis.Shared.Controller.Statistics;

public class GetClubStatisticsResult
{
    /// <summary>Seasons, newest first.</summary>
    public required List<ClubSeasonStatisticsResult> SeasonStatistics { get; init; }
    public required ClubAllTimeStatisticsResult AllTime { get; init; }

    /// <summary>The date the statistics were calculated for. Days after it are not counted yet.</summary>
    public required DateOnly Today { get; init; }
    public required int OpeningHourFrom { get; init; }
    public required int OpeningHourTo { get; init; }
    public required ClubPrimeTimeInfo? PrimeTime { get; init; }
}

public class ClubPrimeTimeInfo
{
    public required int FromHour { get; init; }
    public required int ToHour { get; init; }
    public required List<DayOfWeek> Weekdays { get; init; }
}

public class ClubSeasonStatisticsResult
{
    public required int SeasonId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required string SeasonLabel { get; init; }
    public required SeasonState State { get; init; }

    /// <summary>Total length of the season in days.</summary>
    public required int SeasonLengthDays { get; init; }

    /// <summary>Days of the season up to and including today. Occupancy and day statistics are based on these days.</summary>
    public required int ElapsedDays { get; init; }

    public required ClubOccupancyResult Occupancy { get; init; }
    public required MemberDemographicsResult MemberDemographics { get; init; }
    public required ClubPlayStatsResult PlayStats { get; init; }
    public required ClubMemberActivityResult MemberActivity { get; init; }

    /// <summary>The previous season at the same point in time (same number of elapsed days), null for the first season.</summary>
    public required SeasonPaceComparison? PreviousSeasonPace { get; init; }
}

public enum SeasonState
{
    Past,
    Current,
    Upcoming,
}

public class SeasonPaceComparison
{
    public required string SeasonLabel { get; init; }
    public required double HoursAtSamePoint { get; init; }
    public required int BookingsAtSamePoint { get; init; }
    public required int ActivePlayersAtSamePoint { get; init; }
    public required double TotalHours { get; init; }
}

public class ClubAllTimeStatisticsResult
{
    public required ClubAllTimeTotals Totals { get; init; }
    public required ClubOccupancyResult Occupancy { get; init; }

    /// <summary>One entry per season, oldest first.</summary>
    public required List<ClubSeasonSummary> Seasons { get; init; }
    public required List<PlayModeUsageEntry> PlayModeUsage { get; init; }
    public required List<WeekdayActivityEntry> WeekdayActivity { get; init; }
    public required List<TopPlayerEntry> TopPlayers { get; init; }

    /// <summary>Top players counting only bookings of play modes that count as match.</summary>
    public required List<TopPlayerEntry> TopMatchPlayers { get; init; }
    public required ClubBookingBehaviorResult BookingBehavior { get; init; }

    /// <summary>Booking behavior of bookings of play modes that count as match.</summary>
    public required ClubBookingBehaviorResult MatchBookingBehavior { get; init; }
}

public class ClubAllTimeTotals
{
    public required int Bookings { get; init; }
    public required double Hours { get; init; }
    public required int DistinctPlayers { get; init; }
    public required int DistinctMembers { get; init; }
    public required int Seasons { get; init; }
    public required DateOnly? FirstSeasonStart { get; init; }
    public required double AveragePlayersPerBooking { get; init; }
}

public class ClubSeasonSummary
{
    public required int SeasonId { get; init; }
    public required string SeasonLabel { get; init; }
    public required SeasonState State { get; init; }
    public required int EnrolledMembers { get; init; }
    public required int ActivePlayers { get; init; }
    public required double ActivationRate { get; init; }
    public required int Bookings { get; init; }
    public required double Hours { get; init; }
    public required double OccupancyPercentage { get; init; }
    public required int NewMembers { get; init; }
    public required int ReturningMembers { get; init; }
    public required int LapsedMembers { get; init; }
    public required MemberDemographicsResult Demographics { get; init; }
}

public class ClubOccupancyResult
{
    /// <summary>Number of days the occupancy is based on.</summary>
    public required int Days { get; init; }

    /// <summary>Occupancy of all courts during the opening hours.</summary>
    public required double OverallPercentage { get; init; }

    /// <summary>Occupancy of all courts during prime time, null if prime time is disabled.</summary>
    public required double? PrimeTimePercentage { get; init; }

    public required List<CourtHeatmapResult> CourtHeatmaps { get; init; }

    /// <summary>Occupancy of all courts per weekday and hour, Monday first.</summary>
    public required List<WeekdayHeatmapResult> WeekdayHeatmaps { get; init; }
}

public class CourtHeatmapResult
{
    public required string CourtName { get; init; }
    public required int CourtSortOrder { get; init; }
    public required double OccupancyPercentage { get; init; }
    public required double BookedHours { get; init; }
    public required List<HeatmapHourResult> Hours { get; init; }
}

public class WeekdayHeatmapResult
{
    public required DayOfWeek DayOfWeek { get; init; }
    public required double OccupancyPercentage { get; init; }
    public required List<HeatmapHourResult> Hours { get; init; }
}

public class HeatmapHourResult
{
    public required int Hour { get; init; }
    public required double OccupancyPercentage { get; init; }
    public required double BookedHours { get; init; }
}

public class MemberDemographicsResult
{
    public required int MaleKids { get; init; }
    public required int FemaleKids { get; init; }
    public required int MaleTeenagers { get; init; }
    public required int FemaleTeenagers { get; init; }
    public required int MaleMembers { get; init; }
    public required int FemaleMembers { get; init; }
    public required int MaleSeniors { get; init; }
    public required int FemaleSeniors { get; init; }
    public required int NewMembers { get; init; }
    public required double? AverageAge { get; init; }
    public int TotalMembers => MaleKids + FemaleKids + MaleTeenagers + FemaleTeenagers + MaleMembers + FemaleMembers + MaleSeniors + FemaleSeniors;
    public int TotalMale => MaleKids + MaleTeenagers + MaleMembers + MaleSeniors;
    public int TotalFemale => FemaleKids + FemaleTeenagers + FemaleMembers + FemaleSeniors;
}

public class ClubPlayStatsResult
{
    /// <summary>Bookings that already took place (up to today).</summary>
    public required int TotalBookings { get; init; }
    public required double TotalPlaytimeHours { get; init; }

    /// <summary>Bookings of the season after today.</summary>
    public required int UpcomingBookings { get; init; }
    public required int RecurringBookings { get; init; }
    public required double AveragePlayersPerBooking { get; init; }
    public required double AverageHoursPerDay { get; init; }
    public required BusiestDayEntry? BusiestDay { get; init; }

    public required int DaysWithoutBookings { get; init; }
    public required int DaysUnder3Hours { get; init; }
    public required int Days3To5Hours { get; init; }
    public required int Days5To10Hours { get; init; }
    public required int DaysOver10Hours { get; init; }

    public required List<PlayModeUsageEntry> PlayModeUsage { get; init; }
    public required List<WeekdayActivityEntry> WeekdayActivity { get; init; }
    public required List<WeeklyActivityEntry> WeeklyActivity { get; init; }
    public required ClubBookingBehaviorResult BookingBehavior { get; init; }

    /// <summary>Booking behavior of bookings of play modes that count as match.</summary>
    public required ClubBookingBehaviorResult MatchBookingBehavior { get; init; }
}

public class ClubBookingBehaviorResult
{
    public required int Bookings { get; init; }
    public required int RecurringBookings { get; init; }

    /// <summary>Share of played hours in prime time (0-100), null if prime time is disabled.</summary>
    public required double? PrimeTimeHoursShare { get; init; }
    public required List<LeadTimeBucket> LeadTimes { get; init; }
}

public class BusiestDayEntry
{
    public required DateOnly Date { get; init; }
    public required double Hours { get; init; }
    public required int Bookings { get; init; }
}

public class PlayModeUsageEntry
{
    public required string Name { get; init; }
    public required string Color { get; init; }
    public required double Hours { get; init; }
    public required int Bookings { get; init; }
}

public class WeekdayActivityEntry
{
    public required DayOfWeek DayOfWeek { get; init; }
    public required int Bookings { get; init; }
    public required double Hours { get; init; }
}

public class WeeklyActivityEntry
{
    /// <summary>Monday of the week.</summary>
    public required DateOnly WeekStart { get; init; }
    public required int Bookings { get; init; }
    public required double Hours { get; init; }
    public required int ActivePlayers { get; init; }
}

public enum LeadTimeCategory
{
    LessThanADay,
    OneToTwoDays,
    ThreeToSevenDays,
    MoreThanAWeek,
}

public class LeadTimeBucket
{
    public required LeadTimeCategory Category { get; init; }
    public required int Bookings { get; init; }
}

public class ClubMemberActivityResult
{
    public required int EnrolledMembers { get; init; }

    /// <summary>Enrolled club members with at least one played booking.</summary>
    public required int ActivePlayers { get; init; }

    /// <summary>Share of enrolled members that played at least once (0-100).</summary>
    public required double ActivationRate { get; init; }
    public required int GuestPlayers { get; init; }
    public required int ReturningMembers { get; init; }
    public required int NewMembers { get; init; }

    /// <summary>Members of the previous season that are not enrolled in this season.</summary>
    public required int LapsedMembers { get; init; }
    public required List<ActivityBucket> BookingsPerPlayer { get; init; }
    public required List<TopPlayerEntry> TopPlayers { get; init; }

    /// <summary>Top players counting only bookings of play modes that count as match.</summary>
    public required List<TopPlayerEntry> TopMatchPlayers { get; init; }
}

public enum ActivityBucketCategory
{
    None,
    One,
    TwoToFive,
    SixToTen,
    ElevenToTwenty,
    MoreThanTwenty,
}

public class ActivityBucket
{
    public required ActivityBucketCategory Category { get; init; }
    public required int Members { get; init; }
}

public class TopPlayerEntry
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string? ProfilePictureUrl { get; init; }
    public required bool IsGuest { get; init; }
    public required int Bookings { get; init; }
    public required double Hours { get; init; }
}
