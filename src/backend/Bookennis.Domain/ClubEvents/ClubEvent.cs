using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.ClubEvents;

/// <summary>
/// An event in the club calendar (e.g. a work effort, a party or a meeting).
/// Members can optionally register for it with a head count and answers to the organizer's questions.
/// </summary>
public class ClubEvent : TenantDomainEntity, IAggregateRoot
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxLocationLength = 200;
    public const int MaxQuestionTextLength = 200;
    public const int MaxOptionLabelLength = 100;
    public const int MaxCommentLength = 500;
    public const int MaxQuestions = 20;
    public const int MaxOptionsPerQuestion = 30;
    public const int MaxHeadCount = 50;

    /// <summary>Upper bound of a quantity when it is not limited to the head count (e.g. drinks).</summary>
    public const int MaxQuantity = 99;
    public const int MaxParticipantsLimit = 10_000;

    public enum ErrorCode
    {
        ClubEventTitleRequired = 0,
        ClubEventTextTooLong = 1,
        ClubEventInvalidDateRange = 2,
        ClubEventInvalidTimeRange = 3,
        ClubEventInvalidMaxParticipants = 4,
        ClubEventQuestionTextRequired = 5,
        ClubEventQuestionNeedsOptions = 6,
        ClubEventTooManyQuestions = 7,
        ClubEventOptionLabelRequired = 8,
        ClubEventRegistrationDisabled = 9,
        ClubEventRegistrationClosed = 10,
        ClubEventFull = 11,
        ClubEventInvalidHeadCount = 12,
        ClubEventRequiredQuestionNotAnswered = 13,
        ClubEventInvalidAnswer = 14,
        ClubEventNotRegistered = 15,
    }

    public record EventData(
        string Title,
        string? Description,
        string? Location,
        ClubEventCategory Category,
        DateOnly StartDate,
        DateOnly? EndDate,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        bool RegistrationEnabled,
        int? MaxParticipants,
        DateTimeOffset? RegistrationDeadline,
        IReadOnlyCollection<QuestionData> Questions,
        bool NotifyMembers = true);

    /// <summary><c>LimitQuantityToHeadCount</c>: with quantities, a quantity can't exceed the head count (e.g. one meal per person), otherwise up to <see cref="MaxQuantity"/>.</summary>
    public record QuestionData(int? Id, string Text, ClubEventQuestionSelectionMode SelectionMode, bool IsRequired, bool AllowQuantities, IReadOnlyCollection<OptionData> Options, bool LimitQuantityToHeadCount = true);

    public record OptionData(int? Id, string Label);

    public record AnswerData(int OptionId, int Quantity);

    private readonly List<ClubEventQuestion> questions = new();
    private readonly List<ClubEventRegistration> registrations = new();

#pragma warning disable CS8618
    private ClubEvent() { }
#pragma warning restore CS8618

    public ClubEvent(int clubId, int? createdByMemberId, EventData data)
    {
        ClubId = clubId;
        CreatedByMemberId = createdByMemberId;
        Update(data);
    }

    public int? CreatedByMemberId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public string? Location { get; private set; }
    public ClubEventCategory Category { get; private set; }
    public DateOnly StartDate { get; private set; }

    /// <summary>Last day of a multi-day event; <c>null</c> for single-day events.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Club-local start time; <c>null</c> for all-day events.</summary>
    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }
    public bool RegistrationEnabled { get; private set; }

    /// <summary>Maximum total head count over all registrations; <c>null</c> for unlimited.</summary>
    public int? MaxParticipants { get; private set; }

    public DateTimeOffset? RegistrationDeadline { get; private set; }

    /// <summary>
    /// Whether the club members are told about the event: when it is published and, if they have not registered,
    /// shortly before the registration deadline. Registered members are always reminded of the event.
    /// </summary>
    public bool NotifyMembers { get; private set; } = true;

    /// <summary>When the registered members were reminded of the event.</summary>
    public DateTimeOffset? ReminderSentAt { get; private set; }

    /// <summary>When the members who have not registered were reminded of the registration deadline.</summary>
    public DateTimeOffset? DeadlineReminderSentAt { get; private set; }

    public IReadOnlyList<ClubEventQuestion> Questions => questions.AsReadOnly();
    public IReadOnlyList<ClubEventRegistration> Registrations => registrations.AsReadOnly();

    public DateOnly LastDate => EndDate ?? StartDate;

    public int TotalHeadCount => registrations.Sum(r => r.HeadCount);

    public bool IsFull => MaxParticipants.HasValue && TotalHeadCount >= MaxParticipants.Value;

    /// <summary>The event is over once its last day has passed.</summary>
    public bool IsOver(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime) > LastDate;

    public bool IsDeadlinePassed(DateTimeOffset now) => RegistrationDeadline.HasValue && now > RegistrationDeadline.Value;

    /// <summary>Whether registrations can currently be created, changed or withdrawn (capacity is not considered).</summary>
    public bool IsRegistrationOpen(DateTimeOffset now) => RegistrationEnabled && !IsDeadlinePassed(now) && !IsOver(now);

    /// <summary>
    /// Replaces all event data including the questions. Questions and options are synced by id; answers to removed options are deleted.
    /// Disabling the registration keeps existing registrations.
    /// </summary>
    public void Update(EventData data)
    {
        var title = data.Title?.Trim() ?? "";
        if (title.Length == 0)
            throw new PreconditionException(ErrorCode.ClubEventTitleRequired, "A title is required.");

        var description = Normalize(data.Description);
        var location = Normalize(data.Location);
        if (title.Length > MaxTitleLength || description?.Length > MaxDescriptionLength || location?.Length > MaxLocationLength)
            throw new PreconditionException(ErrorCode.ClubEventTextTooLong, "A text is too long.");

        var endDate = data.EndDate == data.StartDate ? null : data.EndDate;
        if (endDate < data.StartDate)
            throw new PreconditionException(ErrorCode.ClubEventInvalidDateRange, "The end date must not be before the start date.");

        if (data.EndTime.HasValue && !data.StartTime.HasValue)
            throw new PreconditionException(ErrorCode.ClubEventInvalidTimeRange, "An end time requires a start time.");

        if (endDate is null && data.EndTime < data.StartTime)
            throw new PreconditionException(ErrorCode.ClubEventInvalidTimeRange, "The end time must not be before the start time.");

        if (data.MaxParticipants is < 1 or > MaxParticipantsLimit)
            throw new PreconditionException(ErrorCode.ClubEventInvalidMaxParticipants, "Invalid maximum number of participants.");

        // An event or deadline that was moved gets a new reminder
        if (data.StartDate != StartDate || data.StartTime != StartTime)
            ReminderSentAt = null;

        var registrationDeadline = data.RegistrationDeadline?.ToUniversalTime();
        if (registrationDeadline != RegistrationDeadline)
            DeadlineReminderSentAt = null;

        Title = title;
        Description = description;
        Location = location;
        Category = data.Category;
        StartDate = data.StartDate;
        EndDate = endDate;
        StartTime = data.StartTime;
        EndTime = data.StartTime.HasValue ? data.EndTime : null;
        RegistrationEnabled = data.RegistrationEnabled;
        MaxParticipants = data.MaxParticipants;
        RegistrationDeadline = registrationDeadline;
        NotifyMembers = data.NotifyMembers;

        SetQuestions(data.Questions);
    }

    public void MarkReminderSent() => ReminderSentAt = DateTimeOffset.UtcNow;

    public void MarkDeadlineReminderSent() => DeadlineReminderSentAt = DateTimeOffset.UtcNow;

    /// <summary>Creates the member's registration or replaces an existing one.</summary>
    public ClubEventRegistration Register(int memberId, int headCount, IReadOnlyCollection<AnswerData> answers, string? comment, DateTimeOffset now)
    {
        EnsureRegistrationOpen(now);

        if (headCount is < 1 or > MaxHeadCount)
            throw new PreconditionException(ErrorCode.ClubEventInvalidHeadCount, $"The head count must be between 1 and {MaxHeadCount}.");

        comment = Normalize(comment);
        if (comment?.Length > MaxCommentLength)
            throw new PreconditionException(ErrorCode.ClubEventTextTooLong, "The comment is too long.");

        var existing = registrations.FirstOrDefault(r => r.MemberId == memberId);
        if (MaxParticipants.HasValue)
        {
            var otherHeadCount = TotalHeadCount - (existing?.HeadCount ?? 0);
            var remaining = Math.Max(0, MaxParticipants.Value - otherHeadCount);
            if (headCount > remaining)
                throw new PreconditionException(ErrorCode.ClubEventFull, [remaining.ToString()], $"Only {remaining} places are left.");
        }

        var validatedAnswers = ValidateAnswers(headCount, answers);

        if (existing is null)
        {
            var registration = new ClubEventRegistration(this, memberId, headCount, comment, now, validatedAnswers);
            registrations.Add(registration);
            return registration;
        }

        existing.Update(headCount, comment, validatedAnswers);
        return existing;
    }

    public void Unregister(int memberId, DateTimeOffset now)
    {
        var registration = registrations.FirstOrDefault(r => r.MemberId == memberId)
                           ?? throw new PreconditionException(ErrorCode.ClubEventNotRegistered, "You are not registered for this event.");

        if (IsDeadlinePassed(now) || IsOver(now))
            throw new PreconditionException(ErrorCode.ClubEventRegistrationClosed, "The registration is closed.");

        registrations.Remove(registration);
    }

    /// <summary>Removes any registration. Used by organizers, therefore not restricted by the deadline.</summary>
    public void RemoveRegistration(int registrationId)
    {
        var registration = registrations.FirstOrDefault(r => r.Id == registrationId)
                           ?? throw new PreconditionException(ErrorCode.ClubEventNotRegistered, "The registration does not exist.");

        registrations.Remove(registration);
    }

    private void EnsureRegistrationOpen(DateTimeOffset now)
    {
        if (!RegistrationEnabled)
            throw new PreconditionException(ErrorCode.ClubEventRegistrationDisabled, "This event does not accept registrations.");

        if (IsDeadlinePassed(now) || IsOver(now))
            throw new PreconditionException(ErrorCode.ClubEventRegistrationClosed, "The registration is closed.");
    }

    private List<AnswerData> ValidateAnswers(int headCount, IReadOnlyCollection<AnswerData> answers)
    {
        var questionByOptionId = questions.SelectMany(q => q.Options.Select(o => (OptionId: o.Id, Question: q))).ToDictionary(x => x.OptionId, x => x.Question);

        var result = new List<AnswerData>();
        foreach (var answer in answers.GroupBy(a => a.OptionId))
        {
            if (!questionByOptionId.TryGetValue(answer.Key, out var question))
                throw InvalidAnswer("The answer does not belong to this event.");

            var quantity = answer.Sum(a => a.Quantity);
            if (quantity < 1)
                continue;

            if (!question.AllowQuantities)
                quantity = 1;
            else if (question.LimitQuantityToHeadCount && quantity > headCount)
                throw InvalidAnswer($"A quantity must not exceed the head count of {headCount}.");
            else if (quantity > MaxQuantity)
                throw InvalidAnswer($"A quantity must not exceed {MaxQuantity}.");

            result.Add(new AnswerData(answer.Key, quantity));
        }

        foreach (var question in questions)
        {
            var optionIds = question.Options.Select(o => o.Id).ToHashSet();
            var count = result.Count(a => optionIds.Contains(a.OptionId));

            if (question.SelectionMode == ClubEventQuestionSelectionMode.SingleChoice && count > 1)
                throw InvalidAnswer($"Only one option can be selected for '{question.Text}'.");

            if (question.IsRequired && count == 0)
                throw new PreconditionException(ErrorCode.ClubEventRequiredQuestionNotAnswered, [question.Text], $"Please answer '{question.Text}'.");
        }

        return result;
    }

    private static PreconditionException InvalidAnswer(string message) => new(ErrorCode.ClubEventInvalidAnswer, message);

    private void SetQuestions(IReadOnlyCollection<QuestionData> questionData)
    {
        if (questionData.Count > MaxQuestions)
            throw new PreconditionException(ErrorCode.ClubEventTooManyQuestions, $"An event can have at most {MaxQuestions} questions.");

        foreach (var data in questionData)
        {
            var text = data.Text?.Trim() ?? "";
            if (text.Length == 0)
                throw new PreconditionException(ErrorCode.ClubEventQuestionTextRequired, "Every question needs a text.");

            if (text.Length > MaxQuestionTextLength)
                throw new PreconditionException(ErrorCode.ClubEventTextTooLong, "A question is too long.");

            if (data.Options.Count == 0 || data.Options.Count > MaxOptionsPerQuestion)
                throw new PreconditionException(ErrorCode.ClubEventQuestionNeedsOptions, [text], $"The question '{text}' needs between 1 and {MaxOptionsPerQuestion} options.");

            foreach (var option in data.Options)
            {
                var label = option.Label?.Trim() ?? "";
                if (label.Length == 0)
                    throw new PreconditionException(ErrorCode.ClubEventOptionLabelRequired, [text], $"Every option of '{text}' needs a label.");

                if (label.Length > MaxOptionLabelLength)
                    throw new PreconditionException(ErrorCode.ClubEventTextTooLong, "An option is too long.");
            }
        }

        var keptQuestionIds = questionData.Where(d => d.Id.HasValue).Select(d => d.Id!.Value).ToHashSet();
        foreach (var removed in questions.Where(q => !keptQuestionIds.Contains(q.Id)).ToList())
        {
            RemoveAnswersOf(removed.Options.Select(o => o.Id));
            questions.Remove(removed);
        }

        var sortOrder = 0;
        foreach (var data in questionData)
        {
            var existing = data.Id.HasValue ? questions.FirstOrDefault(q => q.Id == data.Id.Value) : null;
            if (existing is null)
            {
                existing = new ClubEventQuestion(this, sortOrder);
                questions.Add(existing);
            }

            var removedOptionIds = existing.Update(data.Text.Trim(), data.SelectionMode, data.IsRequired, data.AllowQuantities, data.LimitQuantityToHeadCount, sortOrder, data.Options);
            RemoveAnswersOf(removedOptionIds);
            sortOrder++;
        }
    }

    private void RemoveAnswersOf(IEnumerable<int> optionIds)
    {
        var ids = optionIds.ToHashSet();
        if (ids.Count == 0)
            return;

        foreach (var registration in registrations)
            registration.RemoveAnswers(ids);
    }

    private static string? Normalize(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
