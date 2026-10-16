using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Infrastructure.User;
using Fusonic.Extensions.Common.Security;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

/// <summary>Players for a booking. Members who are not active only get themselves and the children of their family.</summary>
[Authorize(AuthorizationPolicies.Member)]
public class PlayersController(IMediator mediator, IAuthorizationService authorizationService, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet]
    public async Task<GetPlayersResult> GetClubPlayers([FromRoute] int clubId, CancellationToken cancellationToken)
    {
        if (await authorizationService.IsActiveMember(User))
            return await mediator.Send(new GetPlayers(), cancellationToken);

        var own = await mediator.Send(new GetOwnAndChildMemberIds(clubId, userAccessor.GetUserId()), cancellationToken);
        return await mediator.Send(new GetPlayers(own), cancellationToken);
    }
}