using System.Text;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Memberships;

/// <summary>
/// The endpoint Stripe reports payments to. It is not a club route and takes no sign-in: a request is only accepted
/// with a valid signature of Stripe, and which club an event belongs to is decided by the Stripe account it came from.
/// </summary>
public class StripeController(IMediator mediator, ILogger<StripeController> logger) : ControllerBase
{
    /// <summary>Far more than Stripe sends for an event.</summary>
    private const int MaxPayloadBytes = 512 * 1024;

    [HttpPost("Webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxPayloadBytes)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        // The signature covers the body exactly as it was sent, so it is read as text and not bound to a model
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            await mediator.Send(new ReceiveStripeWebhook(payload, Request.Headers["Stripe-Signature"].ToString()), cancellationToken);
        }
        catch (StripeSignatureException e)
        {
            logger.LogWarning(e, "A request to the Stripe webhook was rejected.");
            return BadRequest();
        }

        return Ok();
    }
}
