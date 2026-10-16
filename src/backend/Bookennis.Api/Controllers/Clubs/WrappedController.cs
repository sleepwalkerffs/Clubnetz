using Bookennis.Api.Business.Wrapped;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Wrapped;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

/// <summary>
/// The season review ("Wrapped") of the signed-in member. Club members (not guests) open the seasons the club admins released;
/// club admins release a season's Wrapped (also of past seasons) and preview the ones that are not released yet.
/// </summary>
public class WrappedController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    [HttpGet]
    [Authorize(AuthorizationPolicies.ClubMember)]
    [Authorize(AuthorizationPolicies.ActiveMember)]
    public async Task<GetWrappedSeasonsResult> GetWrappedSeasons(int clubId, CancellationToken cancellationToken)
        => await mediator.Send(new GetWrappedSeasons(userAccessor.GetUserId(), clubId, await CanManage()), cancellationToken);

    [HttpGet("{seasonId:int}")]
    [Authorize(AuthorizationPolicies.ClubMember)]
    [Authorize(AuthorizationPolicies.ActiveMember)]
    public async Task<SeasonWrappedResult> GetSeasonWrapped(int clubId, int seasonId, CancellationToken cancellationToken)
        => await mediator.Send(new GetSeasonWrapped(userAccessor.GetUserId(), clubId, seasonId, await CanManage()), cancellationToken);

    [HttpPut("{seasonId:int}/publish")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task PublishSeasonWrapped(int clubId, int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new PublishSeasonWrapped(clubId, seasonId), cancellationToken);

    [HttpDelete("{seasonId:int}/publish")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task UnpublishSeasonWrapped(int clubId, int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new UnpublishSeasonWrapped(clubId, seasonId), cancellationToken);

    private async Task<bool> CanManage()
        => (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ClubAdministrator)).Succeeded;
}
