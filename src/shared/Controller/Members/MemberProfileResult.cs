namespace Bookennis.Shared.Controller.Members;

/// <summary>
/// What every member may know about another player: who it is. Nothing else (email, birthday, roles, seasons) is part of it,
/// that is only in <see cref="MembersDetailResult"/> for the member themself and for those who manage the members.
/// </summary>
public record MemberProfileResult
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? ProfilePictureUrl { get; init; }
}
