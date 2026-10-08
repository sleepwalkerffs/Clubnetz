using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;
using Bookennis.Global;

namespace Bookennis.Domain.SubscriptionPlans;

/// <summary>
/// A member's plan for sharing a (winter) court subscription among a group of free-text participants.
/// Holds the planning inputs and the computed (and possibly manually adjusted) weekly schedule.
/// </summary>
public class SubscriptionPlan : TenantDomainEntity, IAggregateRoot
{
    public const int MaxWeeks = 53;
    public const int MaxParticipants = 30;
    public const int MinPlayersPerWeek = 2;
    public const int MaxNameLength = 100;

    public enum ErrorCode
    {
        SubscriptionPlanNameRequired = 0,
        SubscriptionPlanInvalidDateRange = 1,
        SubscriptionPlanDateRangeTooLong = 2,
        SubscriptionPlanInvalidPlayersPerWeek = 3,
        SubscriptionPlanTooManyParticipants = 4,
        SubscriptionPlanParticipantNameRequired = 5,
        SubscriptionPlanDuplicateParticipantName = 6,
        SubscriptionPlanInvalidPercentage = 7,
        SubscriptionPlanNotEnoughParticipants = 8,
        SubscriptionPlanNoActiveWeeks = 9,
        SubscriptionPlanInvalidAssignment = 10,
    }

    public record ParticipantData(int? Id, string Name, int Percentage, int ColorIndex, IReadOnlyCollection<DateOnly> UnavailableWeeks);

    public record WeekAssignmentData(DateOnly Monday, IReadOnlyCollection<int> ParticipantIds);

    private readonly List<SubscriptionPlanParticipant> participants = new();
    private readonly List<SubscriptionPlanAssignment> assignments = new();

#pragma warning disable CS8618
    private SubscriptionPlan() { }
#pragma warning restore CS8618

    public SubscriptionPlan(int clubId, int ownerUserId, string name, DateOnly startDate, DateOnly endDate, int playersPerWeek)
    {
        ClubId = clubId;
        OwnerUserId = ownerUserId;
        SetSettings(name, startDate, endDate, playersPerWeek);
    }

    public int OwnerUserId { get; private set; }
    public string Name { get; private set; } = "";
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public int PlayersPerWeek { get; private set; }

    /// <summary>Mondays of the calendar weeks in which nobody is scheduled.</summary>
    public List<DateOnly> ExcludedWeeks { get; private set; } = new();

    public IReadOnlyList<SubscriptionPlanParticipant> Participants => participants.AsReadOnly();
    public IReadOnlyList<SubscriptionPlanAssignment> Assignments => assignments.AsReadOnly();

    public bool HasSchedule => assignments.Count > 0;

    public IReadOnlyList<CalendarWeek> GetWeeks() => CalendarWeeks.Between(StartDate, EndDate);

    public IReadOnlyList<DateOnly> GetActiveWeeks()
        => GetWeeks().Select(w => w.Monday).Where(monday => !ExcludedWeeks.Contains(monday)).ToList();

    /// <summary>
    /// Replaces all planning inputs. The schedule is cleared when an input relevant for scheduling changed
    /// (renaming a participant or changing a color keeps it).
    /// </summary>
    public void Update(string name, DateOnly startDate, DateOnly endDate, int playersPerWeek, IReadOnlyCollection<DateOnly> excludedWeeks, IReadOnlyCollection<ParticipantData> participantData)
    {
        var scheduleInputsBefore = GetScheduleInputsKey();

        SetSettings(name, startDate, endDate, playersPerWeek);
        ExcludedWeeks = NormalizeWeeks(excludedWeeks);
        SetParticipants(participantData);

        if (GetScheduleInputsKey() != scheduleInputsBefore)
            ClearSchedule();
    }

    public void ClearSchedule() => assignments.Clear();

    /// <summary>Validates that the plan can be scheduled at all.</summary>
    public void EnsureSchedulable()
    {
        if (participants.Count < PlayersPerWeek)
            throw new PreconditionException(ErrorCode.SubscriptionPlanNotEnoughParticipants, $"At least {PlayersPerWeek} participants are required.");

        if (GetActiveWeeks().Count == 0)
            throw new PreconditionException(ErrorCode.SubscriptionPlanNoActiveWeeks, "The plan has no weeks to schedule.");
    }

    /// <summary>Replaces the schedule. Weeks not contained in <paramref name="weeks"/> end up empty.</summary>
    public void SetSchedule(IReadOnlyCollection<WeekAssignmentData> weeks)
    {
        var activeWeeks = GetActiveWeeks().ToHashSet();
        var participantsById = participants.ToDictionary(p => p.Id);

        var newAssignments = new List<SubscriptionPlanAssignment>();
        foreach (var week in weeks.GroupBy(w => w.Monday))
        {
            var ids = week.SelectMany(w => w.ParticipantIds).ToList();
            if (ids.Count == 0)
                continue;

            if (!activeWeeks.Contains(week.Key))
                throw InvalidAssignment($"Week {week.Key} is not part of the plan or is excluded.");

            if (ids.Count > PlayersPerWeek)
                throw InvalidAssignment($"Week {week.Key} has more than {PlayersPerWeek} players.");

            if (ids.Distinct().Count() != ids.Count)
                throw InvalidAssignment($"Week {week.Key} contains a participant more than once.");

            foreach (var id in ids)
            {
                if (!participantsById.TryGetValue(id, out var participant))
                    throw InvalidAssignment($"Participant {id} does not belong to the plan.");

                if (participant.UnavailableWeeks.Contains(week.Key))
                    throw InvalidAssignment($"Participant {participant.Name} is not available in week {week.Key}.");

                newAssignments.Add(new SubscriptionPlanAssignment(this, participant, week.Key));
            }
        }

        assignments.Clear();
        assignments.AddRange(newAssignments);
    }

    private static PreconditionException InvalidAssignment(string message)
        => new(ErrorCode.SubscriptionPlanInvalidAssignment, message);

    private void SetSettings(string name, DateOnly startDate, DateOnly endDate, int playersPerWeek)
    {
        name = name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxNameLength)
            throw new PreconditionException(ErrorCode.SubscriptionPlanNameRequired, "A name is required.");

        if (endDate < startDate)
            throw new PreconditionException(ErrorCode.SubscriptionPlanInvalidDateRange, "The start date must not be after the end date.");

        if (CalendarWeeks.Between(startDate, endDate).Count > MaxWeeks)
            throw new PreconditionException(ErrorCode.SubscriptionPlanDateRangeTooLong, $"A plan can span at most {MaxWeeks} weeks.");

        if (playersPerWeek < MinPlayersPerWeek || playersPerWeek > MaxParticipants)
            throw new PreconditionException(ErrorCode.SubscriptionPlanInvalidPlayersPerWeek, "Invalid number of players per week.");

        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        PlayersPerWeek = playersPerWeek;
    }

    private void SetParticipants(IReadOnlyCollection<ParticipantData> participantData)
    {
        if (participantData.Count > MaxParticipants)
            throw new PreconditionException(ErrorCode.SubscriptionPlanTooManyParticipants, $"A plan can have at most {MaxParticipants} participants.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var data in participantData)
        {
            var participantName = data.Name?.Trim() ?? "";
            if (participantName.Length == 0 || participantName.Length > MaxNameLength)
                throw new PreconditionException(ErrorCode.SubscriptionPlanParticipantNameRequired, "Every participant needs a name.");

            if (!names.Add(participantName))
                throw new PreconditionException(ErrorCode.SubscriptionPlanDuplicateParticipantName, [participantName], $"The participant name '{participantName}' is used more than once.");

            if (data.Percentage is < 1 or > 100)
                throw new PreconditionException(ErrorCode.SubscriptionPlanInvalidPercentage, "The percentage must be between 1 and 100.");
        }

        var keptIds = participantData.Where(d => d.Id.HasValue).Select(d => d.Id!.Value).ToHashSet();
        var removed = participants.Where(p => !keptIds.Contains(p.Id)).ToList();
        foreach (var participant in removed)
        {
            assignments.RemoveAll(a => a.Participant == participant);
            participants.Remove(participant);
        }

        var sortOrder = 0;
        foreach (var data in participantData)
        {
            var unavailableWeeks = NormalizeWeeks(data.UnavailableWeeks);
            var existing = data.Id.HasValue ? participants.FirstOrDefault(p => p.Id == data.Id.Value) : null;
            if (existing is null)
                participants.Add(new SubscriptionPlanParticipant(this, data.Name.Trim(), data.Percentage, data.ColorIndex, sortOrder, unavailableWeeks));
            else
                existing.Update(data.Name.Trim(), data.Percentage, data.ColorIndex, sortOrder, unavailableWeeks);

            sortOrder++;
        }
    }

    /// <summary>Keeps only Mondays of weeks within the plan's date range, distinct and ordered.</summary>
    private List<DateOnly> NormalizeWeeks(IEnumerable<DateOnly> weeks)
    {
        var validMondays = GetWeeks().Select(w => w.Monday).ToHashSet();
        return weeks.Select(CalendarWeeks.GetMonday).Where(validMondays.Contains).Distinct().Order().ToList();
    }

    private string GetScheduleInputsKey()
        => string.Join('|',
            StartDate, EndDate, PlayersPerWeek,
            string.Join(',', ExcludedWeeks.Order()),
            string.Join(';', participants.OrderBy(p => p.Id).Select(p => $"{p.Id}:{p.Percentage}:{string.Join(',', p.UnavailableWeeks.Order())}")));
}
