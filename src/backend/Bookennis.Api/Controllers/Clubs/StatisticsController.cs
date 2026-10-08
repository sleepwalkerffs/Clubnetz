using Bookennis.Api.Business.Statistics;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Statistics;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.ClubMember)]
public class StatisticsController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet]
    public Task<GetMemberStatisticsResult> GetMemberStatistics([FromRoute] int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetMemberStatistics(userAccessor.GetUserId(), clubId), cancellationToken);

    [HttpGet("club")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task<GetClubStatisticsResult> GetClubStatistics([FromRoute] int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubStatistics(clubId), cancellationToken);
}
