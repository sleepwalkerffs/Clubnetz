using Bookennis.Api.Business.Leaderboards;
using Bookennis.Api.Business.Shared;
using Bookennis.Shared.Controller.Shared;
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
    /// <param name="clubId">The club.</param>
    /// <param name="sport">Only bookings on courts of this sport; leave it out for every sport.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet]
    [Authorize(AuthorizationPolicies.ActiveMember)]
    public Task<GetLeaderboardResult> GetLeaderboard(int clubId, [FromQuery] Sport? sport, CancellationToken cancellationToken)
        => mediator.Send(new GetLeaderboard(userAccessor.GetUserId(), clubId, sport.ToDomain()), cancellationToken);

    [HttpPost("opt-out")]
    public Task ToggleOptOut([FromBody] ToggleLeaderboardOptOutRequest request, CancellationToken cancellationToken)
        => mediator.Send(new ToggleLeaderboardOptOut(userAccessor.GetUserId(), request.SeasonId), cancellationToken);
}
