using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;


[Authorize(AuthorizationPolicies.Member)]
public class PlayModesController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public Task<GetPlayModesResult> GetClubPlayers(CancellationToken cancellationToken) => mediator.Send(new GetPlayModes(), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task AddPlayMode(PlayModeRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AddPlayMode(
            request.Name,
            request.AllowedRoles.Select(r => (MemberRole)r).ToArray(),
            request.Color,
            request.FixedPlayerCount,
            request.IsChargingBookingSubscription,
            request.FixedDuration,
            request.CanOverbook,
            request.CommentAllowed,
            request.MaxBookingsPerSeason,
            request.AllowRecurring), cancellationToken);

    [HttpPut("{playModeId:int}")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task UpdatePlayMode(int playModeId, PlayModeRequest request, CancellationToken cancellationToken)
        => mediator.Send(new UpdatePlayMode(
            playModeId,
            request.Name,
            request.AllowedRoles.Select(r => (MemberRole)r).ToArray(),
            request.Color,
            request.FixedPlayerCount,
            request.IsChargingBookingSubscription,
            request.FixedDuration,
            request.CanOverbook,
            request.CommentAllowed,
            request.MaxBookingsPerSeason,
            request.AllowRecurring), cancellationToken);

    [HttpDelete("{playModeId:int}")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task DeletePlayMode(int playModeId, CancellationToken cancellationToken)
        => mediator.Send(new DeletePlayMode(playModeId), cancellationToken);
}
