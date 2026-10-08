namespace Bookennis.Shared.Controller.ClubEvents;

public record RegisterForClubEventModel
{
    /// <summary>Number of people coming, including the member.</summary>
    public int HeadCount { get; init; } = 1;
    public string? Comment { get; init; }
    public List<ClubEventAnswerDto> Answers { get; init; } = [];
}
