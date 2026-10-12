using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Memberships;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Memberships;

/// <summary>
/// The price list of the club: membership plans per season. Club members (not guests) read the plans they can choose,
/// Treasurers and Admins design them.
/// </summary>
public class MembershipPlansController(IMediator mediator, IAuthorizationService authorizationService) : ClubControllerBase
{
    /// <summary>The plans of a season in the order members see them. Treasurers also get the plans only they can assign.</summary>
    [HttpGet]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public async Task<GetMembershipPlansResult> GetMembershipPlans(int clubId, [FromQuery] int seasonId, CancellationToken cancellationToken)
        => await mediator.Send(new GetMembershipPlans(clubId, seasonId, await CanManage()), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.ClubTreasurer)]
    public Task<int> CreateMembershipPlan(int clubId, [FromQuery] int seasonId, [FromBody] SaveMembershipPlanModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateMembershipPlan(clubId, seasonId, model.ToPlanData()), cancellationToken);

    /// <summary>Changes a plan. Orders that exist keep the name and the amount they were created with.</summary>
    [HttpPut("{planId:int}")]
    [Authorize(AuthorizationPolicies.ClubTreasurer)]
    public Task UpdateMembershipPlan(int clubId, int planId, [FromBody] SaveMembershipPlanModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateMembershipPlan(clubId, planId, model.ToPlanData()), cancellationToken);

    /// <summary>Deletes a plan. Rejected with <c>MembershipPlanHasOrders</c> once somebody ordered it.</summary>
    [HttpDelete("{planId:int}")]
    [Authorize(AuthorizationPolicies.ClubTreasurer)]
    public Task DeleteMembershipPlan(int clubId, int planId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteMembershipPlan(clubId, planId), cancellationToken);

    /// <summary>Copies the plans of a season into a season without plans. Birth years and dates move by the difference of the season years.</summary>
    [HttpPost("copy")]
    [Authorize(AuthorizationPolicies.ClubTreasurer)]
    public Task CopyMembershipPlans(int clubId, [FromBody] CopyMembershipPlansModel model, CancellationToken cancellationToken)
        => mediator.Send(new CopyMembershipPlans(clubId, model.SourceSeasonId, model.TargetSeasonId), cancellationToken);

    /// <summary>Sets the order in which members see the plans of a season.</summary>
    [HttpPut("sort")]
    [Authorize(AuthorizationPolicies.ClubTreasurer)]
    public Task SortMembershipPlans(int clubId, [FromBody] SortMembershipPlansModel model, CancellationToken cancellationToken)
        => mediator.Send(new SortMembershipPlans(clubId, model.SeasonId, model.PlanIds ?? []), cancellationToken);

    private async Task<bool> CanManage()
        => (await authorizationService.AuthorizeAsync(HttpContext.User, AuthorizationPolicies.ClubTreasurer)).Succeeded;
}
