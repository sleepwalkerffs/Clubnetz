using Bookennis.Api.Business.SubscriptionPlans;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Shared.Controller.SubscriptionPlans;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.SubscriptionPlans;

[Authorize(AuthorizationPolicies.ClubMember)]
public class SubscriptionPlansController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    [HttpGet]
    public Task<GetSubscriptionPlansResult> GetSubscriptionPlans(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetSubscriptionPlans(clubId, userAccessor.GetUserId()), cancellationToken);

    [HttpGet("{subscriptionPlanId:int}")]
    public async Task<SubscriptionPlanDto> GetSubscriptionPlan(int clubId, int subscriptionPlanId, CancellationToken cancellationToken)
    {
        await EnsureOwnsPlan(subscriptionPlanId);
        return await mediator.Send(new GetSubscriptionPlan(clubId, subscriptionPlanId), cancellationToken);
    }

    [HttpPost]
    public Task<int> CreateSubscriptionPlan(int clubId, [FromBody] CreateSubscriptionPlanModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateSubscriptionPlan(clubId, userAccessor.GetUserId(), model.Name, model.StartDate, model.EndDate, model.PlayersPerWeek), cancellationToken);

    [HttpPut("{subscriptionPlanId:int}")]
    public async Task<SubscriptionPlanDto> UpdateSubscriptionPlan(int clubId, int subscriptionPlanId, [FromBody] UpdateSubscriptionPlanModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnsPlan(subscriptionPlanId);
        return await mediator.Send(new UpdateSubscriptionPlan(
            clubId,
            subscriptionPlanId,
            model.Name,
            model.StartDate,
            model.EndDate,
            model.PlayersPerWeek,
            model.ExcludedWeeks,
            model.Participants.Select(p => new SubscriptionPlan.ParticipantData(p.Id, p.Name, p.Percentage, p.ColorIndex, p.UnavailableWeeks)).ToList()), cancellationToken);
    }

    [HttpDelete("{subscriptionPlanId:int}")]
    public async Task DeleteSubscriptionPlan(int clubId, int subscriptionPlanId, CancellationToken cancellationToken)
    {
        await EnsureOwnsPlan(subscriptionPlanId);
        await mediator.Send(new DeleteSubscriptionPlan(clubId, subscriptionPlanId), cancellationToken);
    }

    [HttpPost("{subscriptionPlanId:int}/compute")]
    public async Task<SubscriptionPlanDto> ComputeSubscriptionSchedule(int clubId, int subscriptionPlanId, [FromBody] ComputeSubscriptionScheduleModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnsPlan(subscriptionPlanId);
        return await mediator.Send(new ComputeSubscriptionSchedule(clubId, subscriptionPlanId, model.Seed), cancellationToken);
    }

    [HttpPut("{subscriptionPlanId:int}/schedule")]
    public async Task<SubscriptionPlanDto> UpdateSubscriptionSchedule(int clubId, int subscriptionPlanId, [FromBody] UpdateSubscriptionScheduleModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnsPlan(subscriptionPlanId);
        return await mediator.Send(new UpdateSubscriptionSchedule(
            clubId,
            subscriptionPlanId,
            model.Weeks.Select(w => new SubscriptionPlan.WeekAssignmentData(w.Monday, w.ParticipantIds)).ToList()), cancellationToken);
    }

    [HttpGet("{subscriptionPlanId:int}/export")]
    public async Task<IActionResult> ExportSubscriptionPlan(int clubId, int subscriptionPlanId, CancellationToken cancellationToken)
    {
        await EnsureOwnsPlan(subscriptionPlanId);
        var result = await mediator.Send(new ExportSubscriptionPlan(clubId, subscriptionPlanId), cancellationToken);
        return File(result.Data, ExportSubscriptionPlanResult.ContentType, result.FileName);
    }

    private Task EnsureOwnsPlan(int subscriptionPlanId)
        => authorizationService.AuthorizeAsync(HttpContext.User, new SubscriptionPlanAuthorizationModel(subscriptionPlanId), AuthorizationPolicies.OwnsSubscriptionPlan).EnsureSucceeded();
}
