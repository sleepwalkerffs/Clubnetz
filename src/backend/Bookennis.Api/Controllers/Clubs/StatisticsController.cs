using Bookennis.Api.Business.Shared;
using Bookennis.Api.Business.Statistics;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Controller.Statistics;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.ClubMember)]
public class StatisticsController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    /// <param name="clubId">The club.</param>
    /// <param name="sport">Only bookings on courts of this sport; leave it out for every sport.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet]
    [Authorize(AuthorizationPolicies.ActiveMember)]
    public Task<GetMemberStatisticsResult> GetMemberStatistics([FromRoute] int clubId, [FromQuery] Sport? sport, CancellationToken cancellationToken)
        => mediator.Send(new GetMemberStatistics(userAccessor.GetUserId(), clubId, sport.ToDomain()), cancellationToken);

    /// <param name="clubId">The club.</param>
    /// <param name="sport">Only the courts of this sport, the bookings on them and the members enrolled for it; leave it out for every sport.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("club")]
    [Authorize(AuthorizationPolicies.ClubStatisticsViewer)]
    public Task<GetClubStatisticsResult> GetClubStatistics([FromRoute] int clubId, [FromQuery] Sport? sport, CancellationToken cancellationToken)
        => mediator.Send(new GetClubStatistics(clubId, Sport: sport.ToDomain()), cancellationToken);
}
