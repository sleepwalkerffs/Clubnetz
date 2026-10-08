namespace Bookennis.Shared.Controller.ClubEvents;

public record GetClubEventsResult
{
    public required List<ClubEventSummaryDto> Events { get; init; }
}

public record ClubEventSummaryDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Location { get; init; }
    public required ClubEventCategory Category { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public required bool RegistrationEnabled { get; init; }
    public int? MaxParticipants { get; init; }
    public DateTimeOffset? RegistrationDeadline { get; init; }
    public required int TotalHeadCount { get; init; }
    public required int RegistrationCount { get; init; }

    /// <summary>Head count of the current member's registration, <c>null</c> if not registered.</summary>
    public int? MyHeadCount { get; init; }

    public DateOnly LastDate => EndDate ?? StartDate;
}
