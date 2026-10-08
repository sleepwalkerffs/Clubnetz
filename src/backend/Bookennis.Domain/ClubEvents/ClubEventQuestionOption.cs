using Bookennis.Domain.Base;

namespace Bookennis.Domain.ClubEvents;

public class ClubEventQuestionOption : DomainEntity
{
#pragma warning disable CS8618
    private ClubEventQuestionOption() { }
#pragma warning restore CS8618

    internal ClubEventQuestionOption(ClubEventQuestion question, string label, int sortOrder)
    {
        Question = question;
        ClubEventQuestionId = question.Id;
        Update(label, sortOrder);
    }

    public ClubEventQuestion Question { get; private set; }
    public int ClubEventQuestionId { get; private set; }
    public string Label { get; private set; } = "";
    public int SortOrder { get; private set; }

    internal void Update(string label, int sortOrder)
    {
        Label = label;
        SortOrder = sortOrder;
    }
}
