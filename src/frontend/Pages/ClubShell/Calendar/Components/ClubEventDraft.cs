using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Client.Pages.ClubShell.Calendar.Components;

/// <summary>Editable state of the event editor, converted to a <see cref="SaveClubEventModel"/> when saving.</summary>
internal sealed class ClubEventDraft
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Location { get; set; }
    public ClubEventCategory Category { get; set; } = ClubEventCategory.WorkEffort;
    public DateTime? StartDate { get; set; } = DateTime.Today.AddDays(7);
    public bool IsMultiDay { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsAllDay { get; set; }
    public TimeSpan? StartTime { get; set; } = new(9, 0, 0);
    public TimeSpan? EndTime { get; set; }
    public bool RegistrationEnabled { get; set; } = true;
    public int? MaxParticipants { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public TimeSpan? DeadlineTime { get; set; }
    public bool NotifyMembers { get; set; } = true;
    public List<QuestionDraft> Questions { get; set; } = [];

    public static ClubEventDraft From(ClubEventDto clubEvent) => new()
    {
        Title = clubEvent.Title,
        Description = clubEvent.Description,
        Location = clubEvent.Location,
        Category = clubEvent.Category,
        StartDate = clubEvent.StartDate.ToDateTime(TimeOnly.MinValue),
        IsMultiDay = clubEvent.EndDate is not null,
        EndDate = clubEvent.EndDate?.ToDateTime(TimeOnly.MinValue),
        IsAllDay = clubEvent.StartTime is null,
        StartTime = clubEvent.StartTime?.ToTimeSpan(),
        EndTime = clubEvent.EndTime?.ToTimeSpan(),
        RegistrationEnabled = clubEvent.RegistrationEnabled,
        MaxParticipants = clubEvent.MaxParticipants,
        DeadlineDate = clubEvent.RegistrationDeadline?.ToLocalTime().Date,
        DeadlineTime = clubEvent.RegistrationDeadline?.ToLocalTime().TimeOfDay,
        NotifyMembers = clubEvent.NotifyMembers,
        Questions = clubEvent.Questions.Select(q => new QuestionDraft
        {
            Id = q.Id,
            Text = q.Text,
            SelectionMode = q.SelectionMode,
            IsRequired = q.IsRequired,
            AllowQuantities = q.AllowQuantities,
            LimitQuantityToHeadCount = q.LimitQuantityToHeadCount,
            Options = q.Options.Select(o => new OptionDraft { Id = o.Id, Label = o.Label }).ToList(),
        }).ToList(),
    };

    public SaveClubEventModel ToModel()
    {
        var startDate = DateOnly.FromDateTime(StartDate ?? DateTime.Today);
        return new SaveClubEventModel
        {
            Title = Title,
            Description = Description,
            Location = Location,
            Category = Category,
            StartDate = startDate,
            EndDate = IsMultiDay && EndDate is { } end ? DateOnly.FromDateTime(end) : null,
            StartTime = IsAllDay || StartTime is null ? null : TimeOnly.FromTimeSpan(StartTime.Value),
            EndTime = IsAllDay || StartTime is null || EndTime is null ? null : TimeOnly.FromTimeSpan(EndTime.Value),
            RegistrationEnabled = RegistrationEnabled,
            MaxParticipants = RegistrationEnabled ? MaxParticipants : null,
            RegistrationDeadline = RegistrationEnabled ? Deadline : null,
            NotifyMembers = NotifyMembers,
            Questions = RegistrationEnabled
                ? Questions.Select(q => new SaveClubEventQuestionModel
                {
                    Id = q.Id,
                    Text = q.Text,
                    SelectionMode = q.SelectionMode,
                    IsRequired = q.IsRequired,
                    AllowQuantities = q.SelectionMode == ClubEventQuestionSelectionMode.MultipleChoice && q.AllowQuantities,
                    LimitQuantityToHeadCount = q.LimitQuantityToHeadCount,
                    Options = q.Options.Select(o => new SaveClubEventOptionModel(o.Id, o.Label)).ToList(),
                }).ToList()
                : [],
        };
    }

    /// <summary>The deadline in the browser's time zone; without a time it ends at the end of the day.</summary>
    private DateTimeOffset? Deadline
    {
        get
        {
            if (DeadlineDate is not { } date)
                return null;

            var local = DateTime.SpecifyKind(date.Date + (DeadlineTime ?? new TimeSpan(23, 59, 0)), DateTimeKind.Local);
            return new DateTimeOffset(local);
        }
    }

    /// <summary>Ids of the options that exist on the server but are no longer part of the draft.</summary>
    public HashSet<int> RemovedOptionIds(ClubEventDto original)
    {
        var kept = RegistrationEnabled
            ? Questions.SelectMany(q => q.Options).Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToHashSet()
            : [];
        return original.Questions.SelectMany(q => q.Options).Select(o => o.Id).Where(id => !kept.Contains(id)).ToHashSet();
    }
}

internal sealed class QuestionDraft
{
    public int? Id { get; set; }
    public string Text { get; set; } = "";
    public ClubEventQuestionSelectionMode SelectionMode { get; set; } = ClubEventQuestionSelectionMode.SingleChoice;
    public bool IsRequired { get; set; }
    public bool AllowQuantities { get; set; }
    public bool LimitQuantityToHeadCount { get; set; } = true;
    public List<OptionDraft> Options { get; set; } = [new(), new()];
}

internal sealed class OptionDraft
{
    public int? Id { get; set; }
    public string Label { get; set; } = "";
}
