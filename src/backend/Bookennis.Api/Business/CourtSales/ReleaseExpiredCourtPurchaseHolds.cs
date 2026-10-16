using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Domain.CourtSales;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>
/// The holds whose payment page closed a while ago (all clubs): <see cref="ReleaseExpiredCourtPurchaseHold"/> releases them one by one.
/// Normally <c>checkout.session.expired</c> releases a hold; this catches the holds whose event did not arrive.
/// </summary>
public record GetExpiredCourtPurchaseHolds : IQuery<List<int>>
{
    /// <summary>Holds are released this long after their payment page closed, so the webhook of a last-minute payment can arrive first.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    public const int MaxPurchasesPerRun = 200;

    public class Handler(AppDbContext context) : IRequestHandler<GetExpiredCourtPurchaseHolds, List<int>>
    {
        public Task<List<int>> Handle(GetExpiredCourtPurchaseHolds request, CancellationToken cancellationToken)
        {
            var limit = DateTimeOffset.UtcNow - Grace;

            // A background job has no tenant: every club
            return context.CourtPurchases.IgnoreQueryFilters()
                .Where(p => p.Status == CourtPurchaseStatus.AwaitingPayment && p.HoldExpiresAt != null && p.HoldExpiresAt < limit)
                .OrderBy(p => p.HoldExpiresAt)
                .Select(p => p.Id)
                .Take(MaxPurchasesPerRun)
                .ToListAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Gives the slot of one purchase free whose buyer did not pay in time, in its own transaction. Stripe is asked first, because the
/// payment page may have been completed (then the payment is booked).
/// </summary>
public record ReleaseExpiredCourtPurchaseHold(int PurchaseId) : ICommand
{
    public class Handler(AppDbContext context, ICourtPurchaseSettlement settlement, IMembershipCheckout checkout) : IRequestHandler<ReleaseExpiredCourtPurchaseHold>
    {
        public async Task<Unit> Handle(ReleaseExpiredCourtPurchaseHold request, CancellationToken cancellationToken)
        {
            var purchase = await context.CourtPurchases.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == request.PurchaseId, cancellationToken);
            if (purchase is not { Status: CourtPurchaseStatus.AwaitingPayment })
                return default;

            var emails = new List<Func<Task>>();
            var result = await settlement.CheckSession(purchase, emails, cancellationToken);
            if (result == SessionCheck.Open && purchase.StripeCheckoutSessionId is { } sessionId)
                await checkout.ExpireSession(purchase.ClubId, sessionId, cancellationToken);

            if (result != SessionCheck.Settled)
                await settlement.ReleaseHold(purchase, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            // Club emails are queued as background jobs that enlist in the transaction of this command: they only go out when it commits
            foreach (var send in emails)
                await send();

            return default;
        }
    }
}

/// <summary>Entry point of the recurring Hangfire job: every hold in its own command (and transaction), a failure only skips that hold.</summary>
public class CourtPurchaseHoldReleaseJob(IMediator mediator, ILogger<CourtPurchaseHoldReleaseJob> logger)
{
    public const string JobId = "court-purchase-hold-release";
    public const string Schedule = "*/5 * * * *";

    public async Task Run()
    {
        foreach (var purchaseId in await mediator.Send(new GetExpiredCourtPurchaseHolds()))
        {
            try
            {
                await mediator.Send(new ReleaseExpiredCourtPurchaseHold(purchaseId));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // E.g. the webhook changed the purchase at the same moment; the next run looks at it again
                logger.LogWarning(e, "The hold of court purchase {PurchaseId} could not be released.", purchaseId);
            }
        }
    }
}
