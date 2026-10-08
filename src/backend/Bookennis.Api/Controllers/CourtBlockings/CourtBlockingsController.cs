using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.CourtBlockings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.CourtBlockings;

/// <summary>
/// Time-based court blockings (tournaments, maintenance, weather). Everybody who sees the booking grid can read the blocked times;
/// Maintainers, SportsDirectors and Admins manage the blockings (not YouthSportsDirectors).
/// </summary>
public class CourtBlockingsController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    [Authorize(AuthorizationPolicies.Member)]
    public Task<GetCourtBlockingOccurrencesResult> GetCourtBlockingOccurrences(int clubId, [FromQuery] DateOnly dayFrom, [FromQuery] DateOnly dayTo, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtBlockingOccurrences(clubId, dayFrom, dayTo), cancellationToken);

    [HttpGet("manage")]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public Task<GetCourtBlockingsResult> GetCourtBlockings(int clubId, [FromQuery] bool includePast, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtBlockings(clubId, includePast), cancellationToken);

    [HttpPost("conflicts")]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public Task<GetCourtBlockingConflictsResult> GetCourtBlockingConflicts(int clubId, [FromBody] SaveCourtBlockingModel model, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtBlockingConflicts(clubId, model.ToBlockingData()), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public Task<int> CreateCourtBlocking(int clubId, [FromBody] SaveCourtBlockingModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateCourtBlocking(clubId, model.ToBlockingData(), model.DeleteConflictingBookings), cancellationToken);

    [HttpPut("{courtBlockingId:int}")]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public Task UpdateCourtBlocking(int clubId, int courtBlockingId, [FromBody] SaveCourtBlockingModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateCourtBlocking(clubId, courtBlockingId, model.ToBlockingData(), model.DeleteConflictingBookings), cancellationToken);

    [HttpDelete("{courtBlockingId:int}")]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public Task DeleteCourtBlocking(int clubId, int courtBlockingId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteCourtBlocking(clubId, courtBlockingId), cancellationToken);
}
