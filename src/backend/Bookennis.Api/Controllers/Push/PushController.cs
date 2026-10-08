using Bookennis.Api.Business.Push;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Configuration;
using Bookennis.Shared.Controller.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookennis.Api.Controllers.Push;

/// <summary>Push notifications of the current user's devices (Web Push).</summary>
[Authorize(AuthorizationPolicies.ApplicationUser)]
public class PushController(IMediator mediator) : ControllerBase
{
    [HttpGet("Configuration")]
    public Task<GetPushConfigurationResult> GetConfiguration(CancellationToken cancellationToken)
        => mediator.Send(new GetPushConfiguration(), cancellationToken);

    [HttpPut("Subscription")]
    public async Task SaveSubscription(SavePushSubscriptionModel model, CancellationToken cancellationToken)
        => await mediator.Send(new SavePushSubscription(model.Endpoint, model.P256dh, model.Auth), cancellationToken);

    [HttpPost("Unsubscribe")]
    public async Task DeleteSubscription(DeletePushSubscriptionModel model, CancellationToken cancellationToken)
        => await mediator.Send(new DeletePushSubscription(model.Endpoint), cancellationToken);

    [HttpPost("Test")]
    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    public async Task SendTest(CancellationToken cancellationToken)
        => await mediator.Send(new SendTestPushNotification(), cancellationToken);
}
