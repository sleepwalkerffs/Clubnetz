namespace Bookennis.Shared.Controller.Leaderboards;

public class GetLeaderboardResult
{
    public required List<SeasonLeaderboardResult> SeasonLeaderboards { get; init; }
    public required LeaderboardData AllTime { get; init; }
}

public class SeasonLeaderboardResult
{
    public required int SeasonId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsOptedOut { get; init; }
    public required LeaderboardData Data { get; init; }
}

public class LeaderboardData
{
    public required List<LeaderboardEntry> Entries { get; init; }
    public required int? CurrentMemberRank { get; init; }
    public required double? CurrentMemberDuration { get; init; }

    /// <summary>The entry of the current member, also when it is not part of <see cref="Entries"/> (rank below the top list).</summary>
    public LeaderboardEntry? CurrentMemberEntry { get; init; }

    /// <summary>Hours the current member is behind the player one rank above (null for rank 1 or unranked).</summary>
    public double? HoursToNextRank { get; init; }

    public int TotalPlayers { get; init; }
    public double TotalHours { get; init; }

    /// <summary>Number of played bookings (not participations).</summary>
    public int TotalMatches { get; init; }
}

public class LeaderboardEntry
{
    public required int Rank { get; init; }
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required double TotalHours { get; init; }
    public int Matches { get; init; }

    /// <summary>Places gained (positive) or lost (negative) in the last 7 days, null for new entries.</summary>
    public int? RankChange { get; init; }

    public required bool IsCurrentMember { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public string? DisplayBadgeImageUrl { get; init; }
    public int? DisplayBadgeLevel { get; init; }
}
