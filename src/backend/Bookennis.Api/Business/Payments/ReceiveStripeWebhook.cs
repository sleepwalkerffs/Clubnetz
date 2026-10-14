using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;

namespace Bookennis.Api.Business.Payments;

/// <summary>
/// Takes a webhook request of Stripe: verifies its signature, remembers the event and hands it to a background job
/// (<see cref="ProcessStripeEvent"/>). Stripe gets its answer right away and delivers an event again when it gets none,
/// so an event that is already known is acknowledged without doing anything.
/// Throws <see cref="StripeSignatureException"/> for a request that Stripe did not send.
/// </summary>
public record ReceiveStripeWebhook(string Payload, string? SignatureHeader) : ICommand
{
    public class Handler(AppDbContext context, IStripeGateway gateway, IMediator mediator) : IRequestHandler<ReceiveStripeWebhook>
    {
        public async Task<Unit> Handle(ReceiveStripeWebhook request, CancellationToken cancellationToken)
        {
            var stripeEvent = gateway.ParseWebhookEvent(request.Payload, request.SignatureHeader);
            await StripeEventInbox.Accept(context, mediator, stripeEvent, cancellationToken);
            return default;
        }
    }
}
