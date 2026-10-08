using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.Member)]
public class PlayersController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public Task<GetPlayersResult> GetClubPlayers(CancellationToken cancellationToken) => mediator.Send(new GetPlayers(), cancellationToken);
}