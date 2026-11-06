using Bookennis.Api.Business.PlayRequests;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.PlayRequests;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.PlayRequests;

/// <summary>
/// "Spielersuche": a member asks other members to play at a given time. The first free court of the request is booked - through the normal
/// booking rules - when the last spot is filled. A request blocks nothing. Signed-in users only, never with an API key, and a request is only
/// visible to the requester and the members who were asked (<see cref="AuthorizationPolicies.TakesPartInPlayRequest"/>).
/// </summary>
[Authorize(AuthorizationPolicies.PlayRequestMember)]
public class PlayRequestsController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    /// <summary>The requests of the caller: their own and those they were asked for.</summary>
    [HttpGet]
    public Task<List<PlayRequestDto>> GetPlayRequests(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetPlayRequests(clubId, userAccessor.GetUserId()), cancellationToken);

    /// <summary>The members the caller can ask (name only).</summary>
    [HttpGet("candidates")]
    public Task<List<PlayRequestCandidateDto>> GetPlayRequestCandidates(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetPlayRequestCandidates(clubId, userAccessor.GetUserId()), cancellationToken);

    [HttpPost]
    public Task<int> CreatePlayRequest(int clubId, [FromBody] CreatePlayRequestModel model, CancellationToken cancellationToken)
        => mediator.Send(
            new CreatePlayRequest(
                clubId,
                userAccessor.GetUserId(),
                model.PlayModeId,
                new DateTimeOffsetInterval(model.From, model.To),
                model.TimeZoneInfoId,
                model.CourtIds,
                model.PlayersNeeded,
                model.InviteeMemberIds,
                model.Comment,
                model.ExpiresAt),
            cancellationToken);

    /// <summary>Accepts the request. The answer says whether this filled the last spot and booked the court.</summary>
    [HttpPost("{playRequestId:int}/accept")]
    public async Task<PlayRequestAcceptResult> AcceptPlayRequest(int clubId, int playRequestId, CancellationToken cancellationToken)
    {
        await authorizationService.AuthorizeAsync(User, new PlayRequestAuthorizationModel(playRequestId), AuthorizationPolicies.TakesPartInPlayRequest).EnsureSucceeded();
        var result = await mediator.Send(new AcceptPlayRequest(clubId, userAccessor.GetUserId(), playRequestId), cancellationToken);
        return new PlayRequestAcceptResult(result.IsFilled, result.BookingId);
    }

    [HttpPost("{playRequestId:int}/decline")]
    public async Task DeclinePlayRequest(int clubId, int playRequestId, CancellationToken cancellationToken)
    {
        await authorizationService.AuthorizeAsync(User, new PlayRequestAuthorizationModel(playRequestId), AuthorizationPolicies.TakesPartInPlayRequest).EnsureSucceeded();
        await mediator.Send(new DeclinePlayRequest(clubId, userAccessor.GetUserId(), playRequestId), cancellationToken);
    }

    /// <summary>Withdraws the request; only its requester can.</summary>
    [HttpDelete("{playRequestId:int}")]
    public async Task CancelPlayRequest(int clubId, int playRequestId, CancellationToken cancellationToken)
    {
        await authorizationService.AuthorizeAsync(User, new PlayRequestAuthorizationModel(playRequestId), AuthorizationPolicies.TakesPartInPlayRequest).EnsureSucceeded();
        await mediator.Send(new CancelPlayRequest(clubId, userAccessor.GetUserId(), playRequestId), cancellationToken);
    }
}

/// <summary>The personal, reusable lists of players of the caller. Nobody else sees or changes them.</summary>
[Authorize(AuthorizationPolicies.PlayRequestMember)]
public class PlayerGroupsController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet]
    public Task<List<PlayerGroupDto>> GetPlayerGroups(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetPlayerGroups(clubId, userAccessor.GetUserId()), cancellationToken);

    [HttpPost]
    public Task<PlayerGroupDto> CreatePlayerGroup(int clubId, [FromBody] SavePlayerGroupModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreatePlayerGroup(clubId, userAccessor.GetUserId(), model.Name, model.MemberIds), cancellationToken);

    [HttpPut("{groupId:int}")]
    public Task<PlayerGroupDto> UpdatePlayerGroup(int clubId, int groupId, [FromBody] SavePlayerGroupModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdatePlayerGroup(clubId, userAccessor.GetUserId(), groupId, model.Name, model.MemberIds), cancellationToken);

    [HttpDelete("{groupId:int}")]
    public Task DeletePlayerGroup(int clubId, int groupId, CancellationToken cancellationToken)
        => mediator.Send(new DeletePlayerGroup(clubId, userAccessor.GetUserId(), groupId), cancellationToken);
}
