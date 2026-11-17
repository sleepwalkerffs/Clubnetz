using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.Member)]
public class CourtsController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public Task<GetCourtsResult> GetCourts(CancellationToken cancellationToken) => mediator.Send(new GetCourts(), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task<int> AddCourt(AddCourtRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AddCourt(request.Name, request.Alias, request.IsIndoor, (Domain.Courts.Sport)request.Sport), cancellationToken);

    [HttpPatch("{courtId:int}/Update")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task EditCourt(int courtId, UpdateCourtRequest updateCourt, CancellationToken cancellationToken)
        => mediator.Send(new UpdateCourt(courtId, updateCourt.Name, updateCourt.Alias, updateCourt.Interval, updateCourt.SortOrder, updateCourt.IsIndoor, (Domain.Courts.Sport?)updateCourt.Sport), cancellationToken);
}