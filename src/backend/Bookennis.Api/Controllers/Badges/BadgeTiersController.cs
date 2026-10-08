using Bookennis.Api.Business.Badges;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.BadgeTiers;
using Bookennis.Api.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetEscapades.AspNetCore.SecurityHeaders;

namespace Bookennis.Api.Controllers.Badges;

[Authorize(AuthorizationPolicies.Member)]
public class BadgeTiersController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public Task<GetBadgeTiersResult> GetBadgeTiers(int clubId, int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new GetBadgeTiers(clubId, seasonId), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task<int> CreateBadgeTier(int clubId, int seasonId, [FromBody] CreateBadgeTierModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateBadgeTier(clubId, seasonId, model.Name, model.Description, model.MatchesRequired), cancellationToken);

    [HttpPut("{tierId:int}")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task UpdateBadgeTier(int clubId, int tierId, [FromBody] UpdateBadgeTierModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateBadgeTier(clubId, tierId, model), cancellationToken);

    [HttpDelete("{tierId:int}")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task DeleteBadgeTier(int clubId, int tierId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteBadgeTier(clubId, tierId), cancellationToken);

    [HttpPost("copy")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task CopyBadgeTiers(int clubId, [FromBody] CopyBadgeTiersModel model, CancellationToken cancellationToken)
        => mediator.Send(new CopyBadgeTiers(clubId, model.SourceSeasonId, model.TargetSeasonId), cancellationToken);

    [HttpPost("{tierId:int}/image")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task UploadBadgeTierImage(int clubId, int tierId, IFormFile file, CancellationToken cancellationToken)
        => mediator.Send(new UploadBadgeTierImage(clubId, tierId, file), cancellationToken);

    [HttpGet("{tierId:int}/image")]
    public async Task<IActionResult> GetBadgeTierImage(int clubId, int tierId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBadgeTierImage(clubId, tierId), cancellationToken);
        if (result is null)
            return NotFound();

        return File(result.Data, result.ContentType);
    }

    // Unauthenticated on purpose: this is the URL embedded in the badge-awarded notification email,
    // which email clients fetch with no session/cookie. Only serves the small decorative badge icon.
    [HttpGet("{tierId:int}/public-image")]
    [AllowAnonymous]
    [SecurityHeadersPolicy(SecurityHeadersConfiguration.PublicAssetPolicy)]
    public async Task<IActionResult> GetPublicBadgeTierImage(int clubId, int tierId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBadgeTierImage(clubId, tierId), cancellationToken);
        if (result is null)
            return NotFound();

        return File(result.Data, result.ContentType);
    }
}
