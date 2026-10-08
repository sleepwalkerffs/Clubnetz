using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.SubscriptionPlans;

public record CreateSubscriptionPlanModel(
    [Required] string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int PlayersPerWeek);
