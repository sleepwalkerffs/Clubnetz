using Bookennis.Global;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Client.Pages.ClubShell.SubscriptionPlanner;

/// <summary>Editable copy of the planning inputs (wizard steps 1-4). Saved as a whole via UpdateSubscriptionPlan.</summary>
public class SubscriptionPlanDraft
{
    public const int MinPlayersPerWeek = 2;
    public const int MaxParticipants = 30;

    public string Name { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int PlayersPerWeek { get; set; } = 4;
    public HashSet<DateOnly> ExcludedWeeks { get; set; } = [];
    public List<ParticipantDraft> Participants { get; set; } = [];

    public IReadOnlyList<CalendarWeek> Weeks
        => StartDate is { } start && EndDate is { } end
            ? CalendarWeeks.Between(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end))
            : [];

    public int ActiveWeekCount => Weeks.Count(w => !ExcludedWeeks.Contains(w.Monday));

    public bool HasValidBasics
        => !string.IsNullOrWhiteSpace(Name) && StartDate is not null && EndDate is not null && StartDate <= EndDate && PlayersPerWeek >= MinPlayersPerWeek;

    public bool HasEnoughParticipants => Participants.Count >= PlayersPerWeek;

    public int NextColorIndex()
    {
        var used = Participants.Select(p => p.ColorIndex).ToHashSet();
        var free = Enumerable.Range(0, SubscriptionPlanColors.Palette.Count).FirstOrDefault(i => !used.Contains(i), -1);
        return free >= 0 ? free : Participants.Count % SubscriptionPlanColors.Palette.Count;
    }

    public static SubscriptionPlanDraft FromDto(SubscriptionPlanDto plan) => new()
    {
        Name = plan.Name,
        StartDate = plan.StartDate.ToDateTime(TimeOnly.MinValue),
        EndDate = plan.EndDate.ToDateTime(TimeOnly.MinValue),
        PlayersPerWeek = plan.PlayersPerWeek,
        ExcludedWeeks = plan.Weeks.Where(w => w.IsExcluded).Select(w => w.Monday).ToHashSet(),
        Participants = plan.Participants.Select(p => new ParticipantDraft
        {
            Id = p.Id,
            Name = p.Name,
            Percentage = p.Percentage,
            ColorIndex = p.ColorIndex,
            UnavailableWeeks = p.UnavailableWeeks.ToHashSet(),
        }).ToList(),
    };

    public UpdateSubscriptionPlanModel ToModel()
    {
        var weeks = Weeks.Select(w => w.Monday).ToHashSet();
        return new UpdateSubscriptionPlanModel(
            Name.Trim(),
            DateOnly.FromDateTime(StartDate ?? DateTime.Today),
            DateOnly.FromDateTime(EndDate ?? DateTime.Today),
            PlayersPerWeek,
            ExcludedWeeks.Where(weeks.Contains).Order().ToList(),
            Participants.Select(p => new SubscriptionParticipantModel(
                p.Id,
                p.Name.Trim(),
                p.Percentage,
                p.ColorIndex,
                p.UnavailableWeeks.Where(weeks.Contains).Order().ToList())).ToList());
    }

    /// <summary>Everything that influences the computed schedule (names and colors don't).</summary>
    public string GetScheduleInputsKey()
    {
        var model = ToModel();
        return string.Join('|',
            model.StartDate, model.EndDate, model.PlayersPerWeek,
            string.Join(',', model.ExcludedWeeks),
            string.Join(';', model.Participants.Select(p => $"{p.Id}:{p.Percentage}:{string.Join(',', p.UnavailableWeeks)}")));
    }

    public string GetStateKey()
    {
        var model = ToModel();
        return GetScheduleInputsKey() + "|" + model.Name + "|" + string.Join(';', model.Participants.Select(p => $"{p.Name}:{p.ColorIndex}"));
    }
}

public class ParticipantDraft
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public int Percentage { get; set; } = 100;
    public int ColorIndex { get; set; }
    public HashSet<DateOnly> UnavailableWeeks { get; set; } = [];

    public SubscriptionPlanColors.ParticipantColor Color => SubscriptionPlanColors.Get(ColorIndex);
}
