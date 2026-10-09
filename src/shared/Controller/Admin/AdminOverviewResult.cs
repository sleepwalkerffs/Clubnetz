namespace Bookennis.Shared.Controller.Admin;

public record AdminOverviewResult
{
    public required int ClubCount { get; init; }
    public required int ClubsWithoutActiveSeason { get; init; }
    public required int UserCount { get; init; }
    public required int UnconfirmedUserCount { get; init; }
    public required int NewUsersLast30Days { get; init; }
    public required int BookingsLast30Days { get; init; }
    public required int UpcomingBookings { get; init; }
    public required List<AdminRecentUser> RecentUsers { get; init; }
}

public record AdminRecentUser
{
    public required int Id { get; init; }
    public required string FullName { get; init; }
    public required string? Email { get; init; }
    public required bool EmailConfirmed { get; init; }
    public required DateTime RegisteredAt { get; init; }
}
