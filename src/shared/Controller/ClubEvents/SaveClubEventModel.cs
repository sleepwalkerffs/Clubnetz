namespace Bookennis.Shared.Controller.ClubEvents;

/// <summary>
/// Create and update model of a club event. Questions and options without id are created, missing ones are removed.
/// Texts are validated by the domain, so empty texts result in a localized error instead of a generic bad request.
/// </summary>
public record SaveClubEventModel
{
    public string? Title { get; init; }

    /// <summary>Markdown.</summary>
    public string? Description { get; init; }

    public string? Location { get; init; }
    public ClubEventCategory Category { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public bool RegistrationEnabled { get; init; }
    public int? MaxParticipants { get; init; }
    public DateTimeOffset? RegistrationDeadline { get; init; }

    /// <summary>Tell the club members about the event (when it is published and before the registration deadline).</summary>
    public bool NotifyMembers { get; init; } = true;
    public List<SaveClubEventQuestionModel> Questions { get; init; } = [];
}

public record PreviewClubEventDescriptionModel
{
    public string? Description { get; init; }
}

public record PreviewClubEventDescriptionResult
{
    public required string Html { get; init; }
}

public record SaveClubEventQuestionModel
{
    public int? Id { get; init; }
    public string? Text { get; init; }
    public ClubEventQuestionSelectionMode SelectionMode { get; init; }
    public bool IsRequired { get; init; }
    public bool AllowQuantities { get; init; }
    public bool LimitQuantityToHeadCount { get; init; } = true;
    public List<SaveClubEventOptionModel> Options { get; init; } = [];
}

public record SaveClubEventOptionModel(int? Id, string? Label);
