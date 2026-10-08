namespace Bookennis.Shared.Controller.SubscriptionPlans;

public record GetSubscriptionPlansResult
{
    public required List<SubscriptionPlanSummaryDto> Plans { get; init; }
}

public record SubscriptionPlanSummaryDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int PlayersPerWeek { get; init; }
    public required int ParticipantCount { get; init; }
    public required bool HasSchedule { get; init; }
}
