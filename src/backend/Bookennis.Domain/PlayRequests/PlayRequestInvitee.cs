using Bookennis.Domain.Base;

namespace Bookennis.Domain.PlayRequests;

/// <summary>A member who was asked and may accept a <see cref="PlayRequest"/>.</summary>
public class PlayRequestInvitee : DomainEntity
{
#pragma warning disable CS8618
    private PlayRequestInvitee() { }
#pragma warning restore CS8618

    internal PlayRequestInvitee(PlayRequest request, int memberId)
    {
        Request = request;
        PlayRequestId = request.Id;
        MemberId = memberId;
        Status = PlayRequestInviteeStatus.Invited;
    }

    public PlayRequest Request { get; private set; }
    public int PlayRequestId { get; private set; }
    public int MemberId { get; private set; }
    public PlayRequestInviteeStatus Status { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    internal void Respond(PlayRequestInviteeStatus status, DateTimeOffset now)
    {
        Status = status;
        RespondedAt = now.ToUniversalTime();
    }
}
