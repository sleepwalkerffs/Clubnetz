namespace Bookennis.Shared.Controller.SubscriptionPlans;

public record SubscriptionPlanDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int PlayersPerWeek { get; init; }
    public required bool HasSchedule { get; init; }
    public required List<SubscriptionWeekDto> Weeks { get; init; }
    public required List<SubscriptionParticipantDto> Participants { get; init; }

    /// <summary>How often each pair of participants shares a week (only pairs with ParticipantId1 &lt; ParticipantId2).</summary>
    public required List<SubscriptionPairDto> Pairs { get; init; }
}

public record SubscriptionWeekDto
{
    public required DateOnly Monday { get; init; }
    public required int WeekNumber { get; init; }
    public required int Year { get; init; }
    public required bool IsExcluded { get; init; }

    /// <summary>True when fewer participants than required are available in this week.</summary>
    public required bool IsUnderstaffed { get; init; }

    public required List<int> ParticipantIds { get; init; }

    public DateOnly Sunday => Monday.AddDays(6);
}

public record SubscriptionParticipantDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int Percentage { get; init; }
    public required int ColorIndex { get; init; }
    public required List<DateOnly> UnavailableWeeks { get; init; }

    /// <summary>Number of weeks the participant is scheduled in.</summary>
    public required int Assigned { get; init; }

    /// <summary>Fair number of weeks derived from the percentage and availability.</summary>
    public required double Target { get; init; }
}

public record SubscriptionPairDto
{
    public required int ParticipantId1 { get; init; }
    public required int ParticipantId2 { get; init; }
    public required int Count { get; init; }
    public required double Expected { get; init; }
}
