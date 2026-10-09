namespace Bookennis.Shared.Controller.Admin;

public record AdminClubResult
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int MemberCount { get; init; }
    public required int PlayModeCount { get; init; }
    public int CourtCount { get; init; }
    public bool HasActiveSeason { get; init; }
    public int BookingsLast30Days { get; init; }

    /// <summary>The club admins, the people to talk to when supporting the club.</summary>
    public List<AdminClubContact> Admins { get; init; } = [];
}

public record AdminClubContact
{
    public required int UserId { get; init; }
    public required string FullName { get; init; }
    public required string? Email { get; init; }
}
