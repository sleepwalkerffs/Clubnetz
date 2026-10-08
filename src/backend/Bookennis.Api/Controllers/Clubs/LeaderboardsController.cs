using Bookennis.Api.Business.Leaderboards;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Leaderboards;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.ClubMember)]
public class LeaderboardsController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet]
    public Task<GetLeaderboardResult> GetLeaderboard(CancellationToken cancellationToken)
        => mediator.Send(new GetLeaderboard(userAccessor.GetUserId()), cancellationToken);

    [HttpPost("opt-out")]
    public Task ToggleOptOut([FromBody] ToggleLeaderboardOptOutRequest request, CancellationToken cancellationToken)
        => mediator.Send(new ToggleLeaderboardOptOut(userAccessor.GetUserId(), request.SeasonId), cancellationToken);
}
