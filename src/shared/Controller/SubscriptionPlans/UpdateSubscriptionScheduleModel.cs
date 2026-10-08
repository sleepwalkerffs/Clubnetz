namespace Bookennis.Shared.Controller.SubscriptionPlans;

public record UpdateSubscriptionScheduleModel(List<SubscriptionWeekAssignmentModel> Weeks);

public record SubscriptionWeekAssignmentModel(DateOnly Monday, List<int> ParticipantIds);
