namespace Bookennis.Shared.Controller.ClubEvents;

public record ClubEventDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    /// <summary>Markdown, for the editor.</summary>
    public string? Description { get; init; }

    /// <summary>The description rendered to sanitized html.</summary>
    public string? DescriptionHtml { get; init; }

    public string? Location { get; init; }
    public required ClubEventCategory Category { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public required bool RegistrationEnabled { get; init; }
    public int? MaxParticipants { get; init; }
    public DateTimeOffset? RegistrationDeadline { get; init; }
    public bool NotifyMembers { get; init; }
    public string? CreatedByName { get; init; }

    /// <summary>Registration (or withdrawal) is currently possible: enabled, deadline not passed, event not over. Capacity is reported separately via <see cref="IsFull"/>.</summary>
    public required bool IsRegistrationOpen { get; init; }

    public required bool IsDeadlinePassed { get; init; }
    public required bool IsOver { get; init; }
    public required bool IsFull { get; init; }
    public required int TotalHeadCount { get; init; }
    public required List<ClubEventQuestionDto> Questions { get; init; }
    public required List<ClubEventRegistrationDto> Registrations { get; init; }
    public ClubEventRegistrationDto? MyRegistration { get; init; }

    public DateOnly LastDate => EndDate ?? StartDate;
}

public record ClubEventQuestionDto
{
    public required int Id { get; init; }
    public required string Text { get; init; }
    public required ClubEventQuestionSelectionMode SelectionMode { get; init; }
    public required bool IsRequired { get; init; }
    public required bool AllowQuantities { get; init; }

    /// <summary>With quantities: a quantity can't exceed the head count, otherwise up to 99.</summary>
    public required bool LimitQuantityToHeadCount { get; init; }
    public required List<ClubEventOptionDto> Options { get; init; }
}

public record ClubEventOptionDto
{
    public required int Id { get; init; }
    public required string Label { get; init; }

    /// <summary>Sum of the quantities over all registrations (number of registrations for questions without quantities).</summary>
    public required int Total { get; init; }
}

public record ClubEventRegistrationDto
{
    public required int Id { get; init; }
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public required int HeadCount { get; init; }
    public string? Comment { get; init; }
    public required DateTimeOffset RegisteredAt { get; init; }
    public required List<ClubEventAnswerDto> Answers { get; init; }
}

public record ClubEventAnswerDto(int OptionId, int Quantity);
