using Bookennis.Domain.Base;

namespace Bookennis.Domain.ClubEvents;

/// <summary>A member's registration for a <see cref="ClubEvent"/>. The head count includes the member.</summary>
public class ClubEventRegistration : DomainEntity
{
    private readonly List<ClubEventRegistrationAnswer> answers = new();

#pragma warning disable CS8618
    private ClubEventRegistration() { }
#pragma warning restore CS8618

    internal ClubEventRegistration(ClubEvent clubEvent, int memberId, int headCount, string? comment, DateTimeOffset registeredAt, IReadOnlyCollection<ClubEvent.AnswerData> answerData)
    {
        Event = clubEvent;
        ClubEventId = clubEvent.Id;
        MemberId = memberId;
        RegisteredAt = registeredAt.ToUniversalTime();
        Update(headCount, comment, answerData);
    }

    public ClubEvent Event { get; private set; }
    public int ClubEventId { get; private set; }
    public int MemberId { get; private set; }
    public int HeadCount { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }

    public IReadOnlyList<ClubEventRegistrationAnswer> Answers => answers.AsReadOnly();

    internal void Update(int headCount, string? comment, IReadOnlyCollection<ClubEvent.AnswerData> answerData)
    {
        HeadCount = headCount;
        Comment = comment;

        answers.RemoveAll(a => answerData.All(d => d.OptionId != a.ClubEventQuestionOptionId));
        foreach (var data in answerData)
        {
            var existing = answers.FirstOrDefault(a => a.ClubEventQuestionOptionId == data.OptionId);
            if (existing is null)
                answers.Add(new ClubEventRegistrationAnswer(this, data.OptionId, data.Quantity));
            else
                existing.Update(data.Quantity);
        }
    }

    internal void RemoveAnswers(IReadOnlySet<int> optionIds) => answers.RemoveAll(a => optionIds.Contains(a.ClubEventQuestionOptionId));
}
