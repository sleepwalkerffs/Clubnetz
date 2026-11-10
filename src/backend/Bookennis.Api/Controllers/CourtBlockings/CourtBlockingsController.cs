using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.CourtBlockings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.CourtBlockings;

/// <summary>
/// Time-based court blockings (tournaments, maintenance, weather). Everybody who sees the booking grid can read the blocked times;
/// who manages the blockings is the permission <c>CourtBlockingsManage</c> (by default Maintainers, SportsDirectors and Admins, changeable by the
/// club admin). A member who got the permission for single courts only changes blockings whose courts are all among them.
/// </summary>
public class CourtBlockingsController(IMediator mediator, ResourcePermissionService resourcePermissions) : ClubControllerBase
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
    public async Task<GetCourtBlockingConflictsResult> GetCourtBlockingConflicts(int clubId, [FromBody] SaveCourtBlockingModel model, CancellationToken cancellationToken)
    {
        await resourcePermissions.EnsureCourtBlockingAsync(HttpContext.User, model.CourtIds, null, cancellationToken);
        return await mediator.Send(new GetCourtBlockingConflicts(clubId, model.ToBlockingData()), cancellationToken);
    }

    [HttpPost]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public async Task<int> CreateCourtBlocking(int clubId, [FromBody] SaveCourtBlockingModel model, CancellationToken cancellationToken)
    {
        await resourcePermissions.EnsureCourtBlockingAsync(HttpContext.User, model.CourtIds, null, cancellationToken);
        return await mediator.Send(new CreateCourtBlocking(clubId, model.ToBlockingData(), model.DeleteConflictingBookings), cancellationToken);
    }

    [HttpPut("{courtBlockingId:int}")]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public async Task UpdateCourtBlocking(int clubId, int courtBlockingId, [FromBody] SaveCourtBlockingModel model, CancellationToken cancellationToken)
    {
        await resourcePermissions.EnsureCourtBlockingAsync(HttpContext.User, model.CourtIds, courtBlockingId, cancellationToken);
        await mediator.Send(new UpdateCourtBlocking(clubId, courtBlockingId, model.ToBlockingData(), model.DeleteConflictingBookings), cancellationToken);
    }

    [HttpDelete("{courtBlockingId:int}")]
    [Authorize(AuthorizationPolicies.CourtBlockingManager)]
    public async Task DeleteCourtBlocking(int clubId, int courtBlockingId, CancellationToken cancellationToken)
    {
        await resourcePermissions.EnsureCourtBlockingAsync(HttpContext.User, [], courtBlockingId, cancellationToken);
        await mediator.Send(new DeleteCourtBlocking(clubId, courtBlockingId), cancellationToken);
    }
}
