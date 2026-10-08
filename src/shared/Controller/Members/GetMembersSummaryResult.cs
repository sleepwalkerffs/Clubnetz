namespace Bookennis.Shared.Controller.Members;

public class GetMembersSummaryResult
{
    public required int TotalMembers { get; init; }
    public required int? ActiveSeasonId { get; init; }
    public required int? PreviousSeasonId { get; init; }

    /// <summary>Members enrolled in the active season.</summary>
    public required int ActiveSeasonMembers { get; init; }

    /// <summary>Members of the active season that were not enrolled in the previous season.</summary>
    public required int NewMembers { get; init; }

    /// <summary>Members of the previous season that are not enrolled in the active season.</summary>
    public required int LapsedMembers { get; init; }
    public required int WithoutEmail { get; init; }
}
