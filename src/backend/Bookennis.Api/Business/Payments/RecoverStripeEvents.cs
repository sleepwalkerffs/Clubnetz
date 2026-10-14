using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>
/// Picks up the events Stripe could not deliver to the webhook, e.g. while the app was down, and processes them like webhook calls
/// (Stripe's recovery path: list the events with <c>delivery_success=false</c>; Stripe keeps them 30 days, but retries only for 3).
/// Events the app already knows are skipped. Runs as a recurring job for every connected club.
/// </summary>
public record RecoverStripeEvents : ICommand
{
    /// <summary>At most this many undelivered events per club and run; the next run continues.</summary>
    public const int MaxEventsPerAccount = 200;

    public class Handler(AppDbContext context, IStripeGateway gateway, IMediator mediator, ILogger<RecoverStripeEvents> logger) : IRequestHandler<RecoverStripeEvents>
    {
        public async Task<Unit> Handle(RecoverStripeEvents request, CancellationToken cancellationToken)
        {
            if (!gateway.IsConfigured)
                return default;

            // A background job has no tenant: every connected club
            var accountIds = await context.ClubPaymentAccounts.IgnoreQueryFilters().Select(a => a.StripeAccountId).ToListAsync(cancellationToken);

            foreach (var accountId in accountIds)
            {
                IReadOnlyList<StripeEventInfo> events;
                try
                {
                    events = await gateway.ListUndeliveredEvents(accountId, ProcessStripeEvent.HandledTypes, MaxEventsPerAccount, cancellationToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "The undelivered Stripe events of account {AccountId} could not be listed.", accountId);
                    continue;
                }

                // Oldest first, so the events of an order are handled in the order they happened
                foreach (var stripeEvent in events.Reverse())
                {
                    try
                    {
                        // The events are listed for this account only: they belong to it, whatever they say
                        if (await StripeEventInbox.Accept(context, mediator, stripeEvent with { AccountId = accountId }, cancellationToken))
                            logger.LogInformation("Stripe event {EventId} ({Type}) was not delivered and is processed now.", stripeEvent.Id, stripeEvent.Type);
                    }
                    catch (StripeSignatureException e)
                    {
                        logger.LogWarning(e, "Stripe event {EventId} of account {AccountId} is invalid and skipped.", stripeEvent.Id, accountId);
                    }
                }
            }

            return default;
        }
    }
}

/// <summary>Entry point of the recurring Hangfire job.</summary>
public class StripeEventRecoveryJob(IMediator mediator)
{
    public const string JobId = "stripe-event-recovery";
    public const string Schedule = "17 * * * *";

    public Task Run() => mediator.Send(new RecoverStripeEvents());
}
