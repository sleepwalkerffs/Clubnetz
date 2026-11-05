using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;
using Bookennis.Global;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.PlayRequests;

public enum PlayRequestStatus
{
    Open = 0,
    Filled = 1,
    Cancelled = 2,
}

public enum PlayRequestInviteeStatus
{
    Invited = 0,
    Accepted = 1,
    Declined = 2,
}

/// <summary>
/// A member looks for players ("Spielersuche"): one exact slot, one or more alternative courts, a play mode and a list of members who may accept.
/// The request <b>blocks nothing</b>. When the last spot is filled the court is booked through the normal booking rules
/// (<see cref="Bookings.BookingDomainService"/>), so a request can never break a booking rule. Only the requester and the invitees can see it.
/// </summary>
public class PlayRequest : TenantDomainEntity, IAggregateRoot
{
    public const int MaxCommentLength = 300;
    public const int MaxInvitees = 30;
    public const int MaxCourts = 20;
    public const int MaxPlayers = 8;

    public enum ErrorCode
    {
        PlayRequestNoCourts = 0,
        PlayRequestInvalidStart = 1,
        PlayRequestInvalidExpiry = 2,
        PlayRequestNoInvitees = 3,
        PlayRequestTooManyInvitees = 4,
        PlayRequestInvalidPlayerCount = 5,
        PlayRequestCommentTooLong = 6,
        PlayRequestNotOpen = 7,
        PlayRequestExpired = 8,
        PlayRequestNotInvited = 9,
        PlayRequestAlreadyAccepted = 10,
        PlayRequestNotRequester = 11,
        PlayRequestOwnRequest = 12,
        PlayRequestMemberNotActive = 13,
        PlayRequestPlayModeNotAllowed = 14,
    }

    public record RequestData(
        int PlayModeId,
        DateTimeOffsetInterval Interval,
        string TimeZoneInfoId,
        IReadOnlyCollection<int> CourtIds,
        int PlayersNeeded,
        IReadOnlyCollection<int> InviteeMemberIds,
        string? Comment,
        DateTimeOffset? ExpiresAt);

    private readonly List<PlayRequestInvitee> invitees = new();

#pragma warning disable CS8618
    private PlayRequest() { }
#pragma warning restore CS8618

    private PlayRequest(int clubId, int requesterMemberId, RequestData data, DateTimeOffset now)
    {
        ClubId = clubId;
        RequesterMemberId = requesterMemberId;
        PlayModeId = data.PlayModeId;
        StartsAt = data.Interval.From.ToUniversalTime();
        EndsAt = data.Interval.To.ToUniversalTime();
        TimeZoneInfoId = data.TimeZoneInfoId;
        CourtIds = data.CourtIds.Distinct().ToList();
        PlayersNeeded = data.PlayersNeeded;
        Comment = data.Comment.CleanNullable();
        ExpiresAt = (data.ExpiresAt ?? data.Interval.From).ToUniversalTime();
        Status = PlayRequestStatus.Open;
        CreatedAt = now.ToUniversalTime();
        foreach (var memberId in data.InviteeMemberIds.Distinct())
            invitees.Add(new PlayRequestInvitee(this, memberId));
    }

    public int RequesterMemberId { get; private set; }
    public int PlayModeId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string TimeZoneInfoId { get; private set; }

    /// <summary>Alternative courts in order of preference: the first one that is free is booked.</summary>
    public List<int> CourtIds { get; private set; }

    /// <summary>All players of the booking including the requester (2 for singles, 4 for doubles).</summary>
    public int PlayersNeeded { get; private set; }

    public string? Comment { get; private set; }

    /// <summary>The request ends here: the start time at the latest, or an earlier time chosen by the requester.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }
    public PlayRequestStatus Status { get; private set; }
    public int? BookingId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<PlayRequestInvitee> Invitees => invitees.AsReadOnly();

    public int AcceptedCount => invitees.Count(i => i.Status == PlayRequestInviteeStatus.Accepted);

    /// <summary>Spots that are still free: all players minus the requester minus those who accepted.</summary>
    public int OpenSpots => PlayersNeeded - 1 - AcceptedCount;

    public bool IsOpenAt(DateTimeOffset now) => Status == PlayRequestStatus.Open && now < ExpiresAt;

    public static PlayRequest Create(int clubId, int requesterMemberId, RequestData data, DateTimeOffset now)
    {
        if (data.CourtIds.Count == 0 || data.CourtIds.Count > MaxCourts)
            throw new PreconditionException(ErrorCode.PlayRequestNoCourts, "Choose at least one court");

        if (data.Interval.From <= now || data.Interval.To <= data.Interval.From)
            throw new PreconditionException(ErrorCode.PlayRequestInvalidStart, "The request has to start in the future");

        if (data.ExpiresAt is not null && (data.ExpiresAt.Value <= now || data.ExpiresAt.Value > data.Interval.From))
            throw new PreconditionException(ErrorCode.PlayRequestInvalidExpiry, "The request has to expire in the future but not after its start");

        if (data.PlayersNeeded < 2 || data.PlayersNeeded > MaxPlayers)
            throw new PreconditionException(ErrorCode.PlayRequestInvalidPlayerCount, "A request needs between 2 and 8 players");

        var inviteeIds = data.InviteeMemberIds.Distinct().Where(id => id != requesterMemberId).ToList();
        if (inviteeIds.Count == 0 || inviteeIds.Count < data.PlayersNeeded - 1)
            throw new PreconditionException(ErrorCode.PlayRequestNoInvitees, "Ask at least as many players as are missing");

        if (inviteeIds.Count > MaxInvitees)
            throw new PreconditionException(ErrorCode.PlayRequestTooManyInvitees, "Too many players asked");

        if (data.Comment.CleanNullable() is { Length: > MaxCommentLength })
            throw new PreconditionException(ErrorCode.PlayRequestCommentTooLong, "The comment is too long");

        return new PlayRequest(clubId, requesterMemberId, data with { InviteeMemberIds = inviteeIds }, now);
    }

    /// <summary>
    /// The invitee says yes. Returns <c>true</c> when this was the last missing player: the caller then books the court and calls
    /// <see cref="MarkFilled"/>. Nothing is saved before the booking succeeded (one transaction), so a failed booking leaves the request as it was.
    /// </summary>
    public bool Accept(int memberId, DateTimeOffset now)
    {
        EnsureOpen(now);
        var invitee = GetInvitee(memberId);
        if (invitee.Status == PlayRequestInviteeStatus.Accepted)
            throw new PreconditionException(ErrorCode.PlayRequestAlreadyAccepted, "Already accepted");

        invitee.Respond(PlayRequestInviteeStatus.Accepted, now);
        return OpenSpots == 0;
    }

    /// <summary>The invitee says no. Others can still accept. Someone who accepted can take it back with a decline as long as it is open.</summary>
    public void Decline(int memberId, DateTimeOffset now)
    {
        EnsureOpen(now);
        GetInvitee(memberId).Respond(PlayRequestInviteeStatus.Declined, now);
    }

    public void MarkFilled(int bookingId, DateTimeOffset now)
    {
        BookingId = bookingId;
        Status = PlayRequestStatus.Filled;
        ClosedAt = now.ToUniversalTime();
    }

    public void Cancel(int memberId, DateTimeOffset now)
    {
        if (memberId != RequesterMemberId)
            throw new PreconditionException(ErrorCode.PlayRequestNotRequester, "Only the requester can cancel");

        if (Status != PlayRequestStatus.Open)
            throw new PreconditionException(ErrorCode.PlayRequestNotOpen, "The request is not open any more");

        Status = PlayRequestStatus.Cancelled;
        ClosedAt = now.ToUniversalTime();
    }

    /// <summary>The members who play: the requester and everybody who accepted.</summary>
    public List<int> PlayerMemberIds()
        => [RequesterMemberId, .. invitees.Where(i => i.Status == PlayRequestInviteeStatus.Accepted).Select(i => i.MemberId)];

    private void EnsureOpen(DateTimeOffset now)
    {
        if (Status != PlayRequestStatus.Open)
            throw new PreconditionException(ErrorCode.PlayRequestNotOpen, "The request is not open any more");

        if (now >= ExpiresAt)
            throw new PreconditionException(ErrorCode.PlayRequestExpired, "The request has expired");
    }

    private PlayRequestInvitee GetInvitee(int memberId)
    {
        if (memberId == RequesterMemberId)
            throw new PreconditionException(ErrorCode.PlayRequestOwnRequest, "You can't answer your own request");

        return invitees.FirstOrDefault(i => i.MemberId == memberId)
            ?? throw new PreconditionException(ErrorCode.PlayRequestNotInvited, "You were not asked");
    }
}
