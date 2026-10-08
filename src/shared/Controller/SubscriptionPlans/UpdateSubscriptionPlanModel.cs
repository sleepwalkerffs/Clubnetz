using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.SubscriptionPlans;

public record UpdateSubscriptionPlanModel(
    [Required] string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int PlayersPerWeek,
    List<DateOnly> ExcludedWeeks,
    List<SubscriptionParticipantModel> Participants);

/// <summary>A participant of the plan; <c>Id</c> is null for participants that are new.</summary>
public record SubscriptionParticipantModel(
    int? Id,
    [Required] string Name,
    int Percentage,
    int ColorIndex,
    List<DateOnly> UnavailableWeeks);
