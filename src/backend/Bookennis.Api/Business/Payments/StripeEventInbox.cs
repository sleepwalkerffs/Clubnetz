using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>Where a Stripe event enters the app, from the webhook or from <see cref="RecoverStripeEvents"/>.</summary>
public static class StripeEventInbox
{
    /// <summary>
    /// Remembers the event and hands it to a background job (<see cref="ProcessStripeEvent"/>). Returns <c>false</c> for an event
    /// the app does not act on or already knows, so an event that arrives twice is handled once.
    /// </summary>
    public static async Task<bool> Accept(AppDbContext context, IMediator mediator, StripeEventInfo stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Id.Length > StripeWebhookEvent.MaxIdLength
         || stripeEvent.Type.Length > StripeWebhookEvent.MaxTypeLength
         || stripeEvent.ObjectId.Length > StripeWebhookEvent.MaxIdLength
         || stripeEvent.AccountId?.Length > StripeWebhookEvent.MaxIdLength)
        {
            throw new StripeSignatureException("The webhook event is invalid.");
        }

        // Events the app does not act on are not stored
        if (!ProcessStripeEvent.HandledTypes.Contains(stripeEvent.Type))
            return false;

        if (await context.StripeWebhookEvents.AnyAsync(e => e.EventId == stripeEvent.Id, cancellationToken))
            return false;

        var entry = new StripeWebhookEvent(stripeEvent.Id, stripeEvent.Type, stripeEvent.AccountId, stripeEvent.ObjectId, DateTimeOffset.UtcNow);
        context.StripeWebhookEvents.Add(entry);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // The same event arrived twice at the same moment: the other request handles it
            context.Entry(entry).State = EntityState.Detached;
            return false;
        }

        await mediator.Send(new ProcessStripeEvent(entry.Id), cancellationToken);
        return true;
    }
}
