using Bookennis.Shared.Controller.Statistics;

namespace Bookennis.Shared.Controller.Wrapped;

public class GetWrappedSeasonsResult
{
    /// <summary>Newest first. Members get the released seasons, club admins every season that has started.</summary>
    public required List<WrappedSeasonDto> Seasons { get; init; }

    /// <summary>The caller may release Wrapped and preview the ones that are not released yet.</summary>
    public required bool CanManage { get; init; }
}

public class WrappedSeasonDto
{
    public required int SeasonId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Matches the signed-in member played in the season.</summary>
    public required int Matches { get; init; }
}

/// <summary>
/// The season review of the signed-in member. Every part is optional: the FE only shows the slides with data.
/// Other members only appear with name and picture (partners).
/// </summary>
public class SeasonWrappedResult
{
    public required int SeasonId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsPublished { get; init; }
    public required string ClubName { get; init; }
    public required string? FirstName { get; init; }

    public required int Matches { get; init; }
    public required double HoursOnCourt { get; init; }
    public required DateOnly? FirstMatch { get; init; }
    public required DateOnly? LastMatch { get; init; }
    public required int ActiveWeeks { get; init; }
    public required int DaysPlayed { get; init; }

    /// <summary>Place by matches among the members enrolled in the season (1 = most matches), only when the member played.</summary>
    public required int? Rank { get; init; }
    public required int EnrolledMembers { get; init; }

    /// <summary>Share of the other enrolled members who played fewer matches.</summary>
    public required int? BetterThanPercentage { get; init; }

    public required List<WrappedPartnerDto> TopPartners { get; init; }
    public required int DistinctPartners { get; init; }

    public required PlayerType? PlayerType { get; init; }
    public required DayOfWeek? FavouriteWeekday { get; init; }
    public required int? FavouriteHour { get; init; }

    public required WrappedCourtDto? FavouriteCourt { get; init; }
    public required int CourtsPlayed { get; init; }

    public required MonthRecord? BusiestMonth { get; init; }
    public required WeekRecord? BestWeek { get; init; }
    public required int LongestWeekStreak { get; init; }
    public required int? LongestBreakDays { get; init; }

    /// <summary>The recurring booking series with the most matches of the member in the season.</summary>
    public required WrappedRegularSlotDto? RegularSlot { get; init; }
    public required int RecurringMatches { get; init; }

    public required List<WrappedBadgeDto> Badges { get; init; }
    public required int EventsAttended { get; init; }

    public required WrappedClubDto Club { get; init; }
}

public class WrappedPartnerDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string? ProfilePictureUrl { get; init; }
    public required int Matches { get; init; }
}

public class WrappedCourtDto
{
    public required string Name { get; init; }
    public required int Matches { get; init; }
    public required int SharePercentage { get; init; }
}

public class WrappedRegularSlotDto
{
    public required DayOfWeek DayOfWeek { get; init; }
    /// <summary>Local time of day. A <see cref="TimeSpan"/>, because the client converts <c>TimeOnly</c> to UTC.</summary>
    public required TimeSpan StartTime { get; init; }
    public required string CourtName { get; init; }
    public required int Matches { get; init; }
}

public class WrappedBadgeDto
{
    public required string Name { get; init; }
    public required string? ImageUrl { get; init; }

    /// <summary><c>null</c> for one-time badges.</summary>
    public required int? Level { get; init; }
    public required DateTimeOffset AwardedAt { get; init; }
}

/// <summary>Anonymous numbers of the whole club in the season.</summary>
public class WrappedClubDto
{
    public required int Matches { get; init; }
    public required double HoursOnCourt { get; init; }
    public required int ActivePlayers { get; init; }
    public required int GuestPlayers { get; init; }
    public required string? BusiestCourt { get; init; }
    public required DayOfWeek? BusiestWeekday { get; init; }
    public required int Events { get; init; }

    /// <summary>Share of all matches of the club the member took part in.</summary>
    public required double OwnSharePercentage { get; init; }
}
