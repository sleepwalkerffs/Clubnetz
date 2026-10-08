using Bookennis.Domain.Base;

namespace Bookennis.Domain.SubscriptionPlans;

/// <summary>A free-text participant of a <see cref="SubscriptionPlan"/>; does not have to be a club member.</summary>
public class SubscriptionPlanParticipant : DomainEntity
{
#pragma warning disable CS8618
    private SubscriptionPlanParticipant() { }
#pragma warning restore CS8618

    internal SubscriptionPlanParticipant(SubscriptionPlan plan, string name, int percentage, int colorIndex, int sortOrder, List<DateOnly> unavailableWeeks)
    {
        Plan = plan;
        SubscriptionPlanId = plan.Id;
        Update(name, percentage, colorIndex, sortOrder, unavailableWeeks);
    }

    public SubscriptionPlan Plan { get; private set; }
    public int SubscriptionPlanId { get; private set; }
    public string Name { get; private set; } = "";

    /// <summary>Relative share of games (1-100). A participant with 100% plays twice as often as one with 50%.</summary>
    public int Percentage { get; private set; }

    public int ColorIndex { get; private set; }
    public int SortOrder { get; private set; }

    /// <summary>Mondays of the calendar weeks in which the participant cannot play.</summary>
    public List<DateOnly> UnavailableWeeks { get; private set; } = new();

    internal void Update(string name, int percentage, int colorIndex, int sortOrder, List<DateOnly> unavailableWeeks)
    {
        Name = name;
        Percentage = percentage;
        ColorIndex = Math.Max(0, colorIndex);
        SortOrder = sortOrder;
        UnavailableWeeks = unavailableWeeks;
    }
}
