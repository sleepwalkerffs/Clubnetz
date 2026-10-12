using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

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

            if (stripeEvent.Id.Length > StripeWebhookEvent.MaxIdLength
             || stripeEvent.Type.Length > StripeWebhookEvent.MaxTypeLength
             || stripeEvent.ObjectId.Length > StripeWebhookEvent.MaxIdLength
             || stripeEvent.AccountId?.Length > StripeWebhookEvent.MaxIdLength)
            {
                throw new StripeSignatureException("The webhook event is invalid.");
            }

            // Events the app does not act on are not stored
            if (!ProcessStripeEvent.HandledTypes.Contains(stripeEvent.Type))
                return default;

            if (await context.StripeWebhookEvents.AnyAsync(e => e.EventId == stripeEvent.Id, cancellationToken))
                return default;

            var entry = new StripeWebhookEvent(stripeEvent.Id, stripeEvent.Type, stripeEvent.AccountId, stripeEvent.ObjectId, DateTimeOffset.UtcNow);
            context.StripeWebhookEvents.Add(entry);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
            {
                // The same event arrived twice at the same moment: the other request handles it
                return default;
            }

            await mediator.Send(new ProcessStripeEvent(entry.Id), cancellationToken);
            return default;
        }
    }
}
