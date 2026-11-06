using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.PlayRequests;

/// <summary>Where a request stands. <see cref="Expired"/> is not stored: an open request whose end has passed is shown as expired.</summary>
public enum PlayRequestState
{
    Open = 0,
    Filled = 1,
    Cancelled = 2,
    Expired = 3,
}

public enum PlayRequestAnswer
{
    Invited = 0,
    Accepted = 1,
    Declined = 2,
}

public record PlayRequestCourtDto(int Id, string Name);

/// <summary>A member who was asked. Only the requester gets the full list, everybody else only sees who accepted.</summary>
public record PlayRequestInviteeDto(int MemberId, string FirstName, string LastName, PlayRequestAnswer Answer);

/// <summary>A request for players. Only the requester and the members who were asked ever get it.</summary>
public record PlayRequestDto
{
    public int Id { get; init; }
    public int RequesterMemberId { get; init; }
    public string RequesterFirstName { get; init; } = "";
    public string RequesterLastName { get; init; } = "";
    public int PlayModeId { get; init; }
    public string PlayModeName { get; init; } = "";
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public string TimeZoneInfoId { get; init; } = "";

    /// <summary>The courts the requester would play on, in order of preference.</summary>
    public List<PlayRequestCourtDto> Courts { get; init; } = [];

    /// <summary>All players including the requester.</summary>
    public int PlayersNeeded { get; init; }
    public int OpenSpots { get; init; }
    public string? Comment { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public PlayRequestState State { get; init; }
    public int? BookingId { get; init; }
    public bool IsMine { get; init; }

    /// <summary>The answer of the caller; <c>null</c> for the requester.</summary>
    public PlayRequestAnswer? MyAnswer { get; init; }

    public List<PlayRequestInviteeDto> Invitees { get; init; } = [];
}

public record CreatePlayRequestModel
{
    [Required]
    public int PlayModeId { get; init; }

    [Required]
    public DateTimeOffset From { get; init; }

    [Required]
    public DateTimeOffset To { get; init; }

    [Required]
    public string TimeZoneInfoId { get; init; } = "";

    /// <summary>Alternative courts in order of preference: the first free one is booked.</summary>
    [Required]
    public List<int> CourtIds { get; init; } = [];

    /// <summary>Only for play modes without a fixed number of players; otherwise the play mode decides.</summary>
    public int? PlayersNeeded { get; init; }

    /// <summary>Who may accept. Usually a saved group, changed for this request.</summary>
    [Required]
    public List<int> InviteeMemberIds { get; init; } = [];

    [MaxLength(300)]
    public string? Comment { get; init; }

    /// <summary>The request ends earlier than its start time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>A member who can be asked: an active member of the club (name only).</summary>
public record PlayRequestCandidateDto(int MemberId, string FirstName, string LastName);

public record PlayerGroupMemberDto(int MemberId, string FirstName, string LastName);

/// <summary>The saved list of players of the caller.</summary>
public record PlayerGroupDto(int Id, string Name, List<PlayerGroupMemberDto> Members);

/// <summary>The answer to accepting a request: <see cref="BookingId"/> is set when this filled the last spot and the court was booked.</summary>
public record PlayRequestAcceptResult(bool IsFilled, int? BookingId);

public record SavePlayerGroupModel
{
    [Required]
    [MaxLength(60)]
    public string Name { get; init; } = "";

    [Required]
    public List<int> MemberIds { get; init; } = [];
}
