using Bookennis.Api.Business.Badges;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Configuration;
using Bookennis.Shared.Controller.OneTimeBadges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetEscapades.AspNetCore.SecurityHeaders;

namespace Bookennis.Api.Controllers.Badges;

[Authorize(AuthorizationPolicies.Member)]
public class OneTimeBadgesController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public Task<GetOneTimeBadgesResult> GetOneTimeBadges(int clubId, int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new GetOneTimeBadges(clubId, seasonId), cancellationToken);

    [HttpGet("season-members")]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task<GetSeasonClubMembersResult> GetSeasonClubMembers(int clubId, int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new GetSeasonClubMembers(clubId, seasonId), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task<int> CreateOneTimeBadge(int clubId, int seasonId, [FromBody] CreateOneTimeBadgeModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateOneTimeBadge(clubId, seasonId, model.Name, model.Description), cancellationToken);

    [HttpPut("{oneTimeBadgeId:int}")]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task UpdateOneTimeBadge(int clubId, int oneTimeBadgeId, [FromBody] UpdateOneTimeBadgeModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateOneTimeBadge(clubId, oneTimeBadgeId, model.Name, model.Description), cancellationToken);

    [HttpDelete("{oneTimeBadgeId:int}")]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task DeleteOneTimeBadge(int clubId, int oneTimeBadgeId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteOneTimeBadge(clubId, oneTimeBadgeId), cancellationToken);

    [HttpPost("copy")]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task CopyOneTimeBadges(int clubId, [FromBody] CopyOneTimeBadgesModel model, CancellationToken cancellationToken)
        => mediator.Send(new CopyOneTimeBadges(clubId, model.SourceSeasonId, model.TargetSeasonId), cancellationToken);

    [HttpPost("{oneTimeBadgeId:int}/image")]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task UploadOneTimeBadgeImage(int clubId, int oneTimeBadgeId, IFormFile file, CancellationToken cancellationToken)
        => mediator.Send(new UploadOneTimeBadgeImage(clubId, oneTimeBadgeId, file), cancellationToken);

    [HttpGet("{oneTimeBadgeId:int}/image")]
    public async Task<IActionResult> GetOneTimeBadgeImage(int clubId, int oneTimeBadgeId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOneTimeBadgeImage(clubId, oneTimeBadgeId), cancellationToken);
        if (result is null)
            return NotFound();

        return File(result.Data, result.ContentType);
    }

    // Unauthenticated on purpose: this is the URL embedded in the badge-awarded notification email,
    // which email clients fetch with no session/cookie. Only serves the small decorative badge icon.
    [HttpGet("{oneTimeBadgeId:int}/public-image")]
    [AllowAnonymous]
    [SecurityHeadersPolicy(SecurityHeadersConfiguration.PublicAssetPolicy)]
    public async Task<IActionResult> GetPublicOneTimeBadgeImage(int clubId, int oneTimeBadgeId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOneTimeBadgeImage(clubId, oneTimeBadgeId), cancellationToken);
        if (result is null)
            return NotFound();

        return File(result.Data, result.ContentType);
    }

    [HttpPost("{oneTimeBadgeId:int}/award")]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task AwardOneTimeBadge(int clubId, int oneTimeBadgeId, [FromBody] AwardOneTimeBadgeModel model, CancellationToken cancellationToken)
        => mediator.Send(new AwardOneTimeBadge(clubId, oneTimeBadgeId, model.MemberIds), cancellationToken);

    [HttpDelete("{oneTimeBadgeId:int}/award/{memberId:int}")]
    [Authorize(AuthorizationPolicies.OneTimeBadgeManager)]
    public Task RevokeOneTimeBadge(int clubId, int oneTimeBadgeId, int memberId, CancellationToken cancellationToken)
        => mediator.Send(new RevokeOneTimeBadge(clubId, oneTimeBadgeId, memberId), cancellationToken);
}
