using Bookennis.Domain.Base;

namespace Bookennis.Domain.SubscriptionPlans;

/// <summary>A participant scheduled to play in a calendar week (identified by its Monday).</summary>
public class SubscriptionPlanAssignment : DomainEntity
{
#pragma warning disable CS8618
    private SubscriptionPlanAssignment() { }
#pragma warning restore CS8618

    internal SubscriptionPlanAssignment(SubscriptionPlan plan, SubscriptionPlanParticipant participant, DateOnly weekStart)
    {
        Plan = plan;
        SubscriptionPlanId = plan.Id;
        Participant = participant;
        SubscriptionPlanParticipantId = participant.Id;
        WeekStart = weekStart;
    }

    public SubscriptionPlan Plan { get; private set; }
    public int SubscriptionPlanId { get; private set; }
    public SubscriptionPlanParticipant Participant { get; private set; }
    public int SubscriptionPlanParticipantId { get; private set; }
    public DateOnly WeekStart { get; private set; }
}
