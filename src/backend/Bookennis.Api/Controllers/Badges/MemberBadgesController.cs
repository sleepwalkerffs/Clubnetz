using Bookennis.Api.Business.Badges;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.MemberBadges;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Badges;

[Authorize(AuthorizationPolicies.Member)]
public class MemberBadgesController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    [HttpGet("Members/{memberId:int}/badges")]
    public Task<GetMemberBadgesResult> GetMemberBadges(int clubId, int memberId, [FromQuery] int? seasonId, CancellationToken cancellationToken)
        => mediator.Send(new GetMemberBadges(memberId, clubId, seasonId), cancellationToken);

    [HttpGet("Members/{memberId:int}/badge-progress")]
    public async Task<GetMemberBadgeProgressResult> GetMemberBadgeProgress(int clubId, int memberId, CancellationToken cancellationToken)
    {
        (await authorizationService.AuthorizeAsync(HttpContext.User, new MemberAuthorizationModel(memberId), AuthorizationPolicies.Member)).EnsureSucceeded();
        return await mediator.Send(new GetMemberBadgeProgress(memberId, clubId), cancellationToken);
    }

    [HttpGet("Members/{memberId:int}/trophy-case")]
    public Task<GetTrophyCaseResult> GetTrophyCase(int memberId, CancellationToken cancellationToken)
        => mediator.Send(new GetTrophyCase(userAccessor.GetUserId(), memberId), cancellationToken);

    [HttpPut("Members/{memberId:int}/badge-settings")]
    public async Task UpdateMemberBadgeSettings(int memberId, [FromBody] UpdateMemberBadgeSettingsModel model, CancellationToken cancellationToken)
    {
        (await authorizationService.AuthorizeAsync(HttpContext.User, new MemberAuthorizationModel(memberId), AuthorizationPolicies.Member)).EnsureSucceeded();
        await mediator.Send(new UpdateMemberBadgeSettings(memberId, model), cancellationToken);
    }
}
