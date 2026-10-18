using Bookennis.Api.Business.Clubhouse;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubhouse;

/// <summary>
/// The two addresses SumUp sends to. Neither is a club route: the club of the OAuth callback comes from its encrypted <c>state</c>, the
/// one of a webhook from the checkout it names.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public class SumUpController(IMediator mediator, IUserAccessor userAccessor) : ControllerBase
{
    /// <summary>
    /// The host comes back from SumUp after allowing (or refusing) the access. The browser is always sent on to a page of the app.
    /// Only the signed-in user who started the connection can complete it.
    /// </summary>
    [HttpGet("Callback")]
    [Authorize(AuthorizationPolicies.AccountOwner)]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new CompleteClubhouseSumUpConnect(userAccessor.GetUserId(), code, state, error), cancellationToken);
        return LocalRedirect(page);
    }

    /// <summary>
    /// SumUp reports that a checkout changed. The request is not signed and carries only the checkout id, so it never changes anything
    /// by itself: the checkout is looked up at SumUp. Always answers 200, SumUp retries anything else.
    /// </summary>
    [HttpPost("Webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Webhook([FromBody] SumUpWebhookModel? model, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReceiveSumUpWebhook(model?.EventType, model?.Id), cancellationToken);
        return Ok();
    }
}

/// <summary>What SumUp posts: <c>{"event_type":"CHECKOUT_STATUS_CHANGED","id":"..."}</c>.</summary>
public record SumUpWebhookModel
{
    [System.Text.Json.Serialization.JsonPropertyName("event_type")]
    public string? EventType { get; init; }

    public string? Id { get; init; }
}
