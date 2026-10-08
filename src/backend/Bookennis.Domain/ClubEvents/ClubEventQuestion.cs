using Bookennis.Domain.Base;

namespace Bookennis.Domain.ClubEvents;

/// <summary>A question the organizer asks when members register, e.g. "Staying for food?" or "Which drinks?".</summary>
public class ClubEventQuestion : DomainEntity
{
    private readonly List<ClubEventQuestionOption> options = new();

#pragma warning disable CS8618
    private ClubEventQuestion() { }
#pragma warning restore CS8618

    internal ClubEventQuestion(ClubEvent clubEvent, int sortOrder)
    {
        Event = clubEvent;
        ClubEventId = clubEvent.Id;
        SortOrder = sortOrder;
    }

    public ClubEvent Event { get; private set; }
    public int ClubEventId { get; private set; }
    public string Text { get; private set; } = "";
    public ClubEventQuestionSelectionMode SelectionMode { get; private set; }
    public bool IsRequired { get; private set; }

    /// <summary>Only for multiple selection: members enter how many of their group want an option (e.g. 2x Schnitzel).</summary>
    public bool AllowQuantities { get; private set; }

    /// <summary>With quantities: a quantity can't exceed the head count of the registration (e.g. one meal per person).</summary>
    public bool LimitQuantityToHeadCount { get; private set; } = true;

    public int SortOrder { get; private set; }

    public IReadOnlyList<ClubEventQuestionOption> Options => options.AsReadOnly();

    /// <summary>Updates the question and syncs its options by id. Returns the ids of removed options.</summary>
    internal List<int> Update(string text, ClubEventQuestionSelectionMode selectionMode, bool isRequired, bool allowQuantities, bool limitQuantityToHeadCount, int sortOrder, IReadOnlyCollection<ClubEvent.OptionData> optionData)
    {
        Text = text;
        SelectionMode = selectionMode;
        IsRequired = isRequired;
        AllowQuantities = selectionMode == ClubEventQuestionSelectionMode.MultipleChoice && allowQuantities;
        LimitQuantityToHeadCount = !AllowQuantities || limitQuantityToHeadCount;
        SortOrder = sortOrder;

        var keptIds = optionData.Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToHashSet();
        var removed = options.Where(o => !keptIds.Contains(o.Id)).ToList();
        foreach (var option in removed)
            options.Remove(option);

        var optionSortOrder = 0;
        foreach (var data in optionData)
        {
            var existing = data.Id.HasValue ? options.FirstOrDefault(o => o.Id == data.Id.Value) : null;
            if (existing is null)
                options.Add(new ClubEventQuestionOption(this, data.Label.Trim(), optionSortOrder));
            else
                existing.Update(data.Label.Trim(), optionSortOrder);

            optionSortOrder++;
        }

        return removed.Select(o => o.Id).ToList();
    }
}
