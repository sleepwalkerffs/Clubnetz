using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.Member)]
public class SeasonsController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public Task<GetSeasonsResult> GetSeasons(CancellationToken cancellationToken)
        => mediator.Send(new GetSeasons(), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task<int> AddSeason(SeasonRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AddSeason(request.StartDate, request.EndDate), cancellationToken);

    [HttpPut("{seasonId:int}")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task UpdateSeason(int seasonId, SeasonRequest request, CancellationToken cancellationToken)
        => mediator.Send(new UpdateSeason(seasonId, request.StartDate, request.EndDate), cancellationToken);

    [HttpDelete("{seasonId:int}")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task DeleteSeason(int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteSeason(seasonId), cancellationToken);
}
