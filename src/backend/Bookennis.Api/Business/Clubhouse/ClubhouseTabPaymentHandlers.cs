using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Clubhouse.SumUp;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Clubhouse;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.Hangfire;
using Microsoft.EntityFrameworkCore;
using ClubhouseTabPaymentStatus = Bookennis.Domain.Clubhouse.ClubhouseTabPaymentStatus;
using ClubhouseTabStatus = Bookennis.Domain.Clubhouse.ClubhouseTabStatus;
using SharedPaymentStatus = Bookennis.Shared.Controller.Clubhouse.ClubhouseTabPaymentStatus;

namespace Bookennis.Api.Business.Clubhouse;

/// <summary>
/// The member (or a parent for a child) pays an open tab in the app: the tab is locked, a checkout for its amount is created on the
/// host's SumUp account and the member is sent to SumUp's payment page. Tapping "Pay" again while that page is open leads to the same
/// page. Who may pay the tab is checked by the controller (<c>PaysClubhouseTab</c>).
/// </summary>
public record StartClubhouseTabPayment(int ClubId, int UserId, int TabId) : ICommand<ClubhouseRedirectResult>
{
    /// <summary>A payment page that runs out sooner is not handed out again: the member would land on an expired page.</summary>
    public static readonly TimeSpan MinRemaining = TimeSpan.FromMinutes(3);

    public class Handler(
        AppDbContext context,
        ISumUpGateway gateway,
        ISumUpAccessTokens tokens,
        AppSettings appSettings,
        ILogger<StartClubhouseTabPayment> logger) : IRequestHandler<StartClubhouseTabPayment, ClubhouseRedirectResult>
    {
        public async Task<ClubhouseRedirectResult> Handle(StartClubhouseTabPayment request, CancellationToken cancellationToken)
        {
            var account = gateway.IsConfigured
                ? await context.ClubhouseSumUpAccounts.FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken)
                : null;
            if (account is null || account.NeedsReconnect)
                throw new PreconditionException(ClubhouseErrorCode.ClubhouseOnlinePaymentNotAvailable, "The host has not connected a SumUp account.");

            var tab = await context.GetLockedClubhouseTab(request.ClubId, request.TabId, cancellationToken);
            if (tab.MemberId is null)
                throw new PreconditionException(ClubhouseErrorCode.ClubhouseTabNotPayableOnline, "The tab of a guest from outside can't be paid in the app.");

            var now = DateTimeOffset.UtcNow;
            if (tab.Status == ClubhouseTabStatus.AwaitingPayment)
            {
                var running = await context.ClubhouseTabPayments
                    .FirstOrDefaultAsync(p => p.TabId == tab.Id && p.Status == ClubhouseTabPaymentStatus.Pending, cancellationToken);

                if (running?.CheckoutUrl is { } url && running.ExpiresAt - MinRemaining > now)
                    return new ClubhouseRedirectResult { Url = url };

                // The page runs out in a moment: the job unlocks the tab, then the member can start again
                throw new PreconditionException(ClubhouseErrorCode.ClubhouseTabPaymentRunning, "The tab is being paid online.");
            }

            tab.StartOnlinePayment();
            var payerId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var payment = new ClubhouseTabPayment(tab, payerId, account.MerchantCode, now);
            context.ClubhouseTabPayments.Add(payment);

            // The id goes into the address the member comes back to
            await context.SaveChangesAsync(cancellationToken);

            SumUpCheckout checkout;
            try
            {
                var accessToken = await tokens.Get(account, cancellationToken);
                checkout = await gateway.CreateCheckout(accessToken, new SumUpCheckoutData(
                    account.MerchantCode,
                    payment.CheckoutReference,
                    payment.AmountCents,
                    payment.Currency,
                    await Description(tab, cancellationToken),
                    payment.ExpiresAt,
                    appSettings.SumUpWebhookUrl(),
                    $"{appSettings.AppUri.AbsoluteUri.TrimEnd('/')}/clubs/{request.ClubId}/my-club?clubhouseTab={tab.Id}&clubhousePayment={payment.Id}"),
                    cancellationToken);
            }
            catch (SumUpException e)
            {
                // The transaction is rolled back by the error anyway; unlocking explicitly keeps the tab usable if it is not
                payment.Expire();
                tab.EndOnlinePayment();
                await context.SaveChangesAsync(cancellationToken);

                if (e is SumUpAuthorizationException)
                {
                    logger.LogWarning(e, "A tab of club {ClubId} could not be paid online: the SumUp account has to be connected again.", request.ClubId);
                    throw new PreconditionException(ClubhouseErrorCode.ClubhouseOnlinePaymentNotAvailable, "The SumUp account of the host has to be connected again.");
                }

                throw new PreconditionException(ClubhouseErrorCode.ClubhouseSumUpUnavailable, [e.Message], "SumUp declined the request or could not be reached.");
            }

            payment.AttachCheckout(checkout.Id, checkout.HostedCheckoutUrl!);
            await context.SaveChangesAsync(cancellationToken);
            return new ClubhouseRedirectResult { Url = checkout.HostedCheckoutUrl! };
        }

        /// <summary>What the host sees in the SumUp dashboard next to the payment, e.g. "Clubhouse tab 42 · Anna Huber".</summary>
        private async Task<string> Description(ClubhouseTab tab, CancellationToken cancellationToken)
        {
            var guests = await context.LoadGuests(tab.ClubId, [tab.MemberId!.Value], cancellationToken);
            var name = ClubhouseQueries.Name(tab, guests.GetValueOrDefault(tab.MemberId.Value));
            return string.IsNullOrEmpty(name) ? $"Clubhouse tab {tab.Id}" : $"Clubhouse tab {tab.Id} · {name}";
        }
    }
}

/// <summary>
/// An online payment of a tab, for the member coming back from SumUp. SumUp is asked first (the redirect proves nothing): a paid
/// payment is booked at once, so the member sees the tab paid even when the webhook comes later.
/// </summary>
public record GetClubhouseTabPayment(int ClubId, int TabId, int PaymentId) : ICommand<ClubhouseTabPaymentDto>
{
    public class Handler(AppDbContext context, IClubhouseTabPaymentSettlement settlement) : IRequestHandler<GetClubhouseTabPayment, ClubhouseTabPaymentDto>
    {
        public async Task<ClubhouseTabPaymentDto> Handle(GetClubhouseTabPayment request, CancellationToken cancellationToken)
        {
            var payment = await context.ClubhouseTabPayments
                .FirstOrDefaultAsync(p => p.Id == request.PaymentId && p.TabId == request.TabId && p.ClubId == request.ClubId, cancellationToken)
                ?? throw new EntityNotFoundException(typeof(ClubhouseTabPayment));

            var emails = new List<Func<Task>>();
            if (payment.Status != ClubhouseTabPaymentStatus.Paid)
            {
                await settlement.Check(payment, closeWhenUnpaid: false, emails, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            // Club emails are queued as background jobs that enlist in the transaction: they only go out when it commits
            foreach (var send in emails)
                await send();

            return new ClubhouseTabPaymentDto
            {
                Id = payment.Id,
                TabId = payment.TabId,
                Status = (SharedPaymentStatus)payment.Status,
                AmountCents = payment.AmountCents,
                Currency = payment.Currency,
                PaidAt = payment.PaidAt,
            };
        }
    }
}

/// <summary>
/// SumUp reported that a checkout changed (the webhook). The request carries nothing but the id of the checkout and is not signed,
/// so it only leads to asking SumUp: an unknown id is ignored, a known one is checked in the background.
/// </summary>
public record ReceiveSumUpWebhook(string? EventType, string? CheckoutId) : ICommand
{
    public const string CheckoutStatusChanged = "CHECKOUT_STATUS_CHANGED";

    public class Handler(AppDbContext context, IMediator mediator) : IRequestHandler<ReceiveSumUpWebhook>
    {
        public async Task<Unit> Handle(ReceiveSumUpWebhook request, CancellationToken cancellationToken)
        {
            // SumUp may add other events at any time: they are ignored
            if (request.EventType != CheckoutStatusChanged || string.IsNullOrWhiteSpace(request.CheckoutId) || request.CheckoutId.Length > ClubhouseTabPayment.MaxCheckoutIdLength)
                return default;

            // Not a club route: the payment is found by the checkout id of SumUp, in whichever club it is
            var paymentId = await context.ClubhouseTabPayments.IgnoreQueryFilters()
                .Where(p => p.CheckoutId == request.CheckoutId && p.Status != ClubhouseTabPaymentStatus.Paid)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (paymentId is not null)
                await mediator.Send(new CheckClubhouseTabPayment(paymentId.Value), cancellationToken);

            return default;
        }
    }
}

/// <summary>Asks SumUp about one payment and books the answer (paid, expired). Runs in the background for the webhook.</summary>
public record CheckClubhouseTabPayment(int PaymentId, bool CloseWhenUnpaid = false) : ICommand
{
    // Runs as a background job, so SumUp gets its answer right away
    [OutOfBand]
    public class Handler(AppDbContext context, IClubhouseTabPaymentSettlement settlement) : IRequestHandler<CheckClubhouseTabPayment>
    {
        public async Task<Unit> Handle(CheckClubhouseTabPayment request, CancellationToken cancellationToken)
            => await CheckClubhouseTabPayments.Run(context, settlement, request.PaymentId, request.CloseWhenUnpaid, cancellationToken);
    }
}

/// <summary>
/// Closes one payment page that ran out, in its own transaction: SumUp is asked first, because the member may have paid in the last
/// moment (then the payment is booked). Otherwise the page is closed at SumUp and the tab unlocked. Catches payments whose webhook and
/// return never arrived.
/// </summary>
public record ReleaseExpiredClubhouseTabPayment(int PaymentId) : ICommand
{
    public class Handler(AppDbContext context, IClubhouseTabPaymentSettlement settlement) : IRequestHandler<ReleaseExpiredClubhouseTabPayment>
    {
        public async Task<Unit> Handle(ReleaseExpiredClubhouseTabPayment request, CancellationToken cancellationToken)
            => await CheckClubhouseTabPayments.Run(context, settlement, request.PaymentId, closeWhenUnpaid: true, cancellationToken);
    }
}

/// <summary>The payment pages that ran out a while ago and are still pending (all clubs).</summary>
public record GetExpiredClubhouseTabPayments : IQuery<List<int>>
{
    /// <summary>Pages are closed this long after they ran out, so the webhook of a last-minute payment can arrive first.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    public const int MaxPaymentsPerRun = 200;

    public class Handler(AppDbContext context) : IRequestHandler<GetExpiredClubhouseTabPayments, List<int>>
    {
        public Task<List<int>> Handle(GetExpiredClubhouseTabPayments request, CancellationToken cancellationToken)
        {
            var limit = DateTimeOffset.UtcNow - Grace;

            // A background job has no tenant: every club
            return context.ClubhouseTabPayments.IgnoreQueryFilters()
                .Where(p => p.Status == ClubhouseTabPaymentStatus.Pending && p.ExpiresAt < limit)
                .OrderBy(p => p.ExpiresAt)
                .Select(p => p.Id)
                .Take(MaxPaymentsPerRun)
                .ToListAsync(cancellationToken);
        }
    }
}

public static class CheckClubhouseTabPayments
{
    public static async Task<Unit> Run(AppDbContext context, IClubhouseTabPaymentSettlement settlement, int paymentId, bool closeWhenUnpaid, CancellationToken cancellationToken)
    {
        var payment = await context.ClubhouseTabPayments.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null || payment.Status == ClubhouseTabPaymentStatus.Paid)
            return default;

        var emails = new List<Func<Task>>();
        await settlement.Check(payment, closeWhenUnpaid, emails, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var send in emails)
            await send();

        return default;
    }
}

/// <summary>Entry point of the recurring Hangfire job: every payment in its own command (and transaction), a failure only skips that one.</summary>
public class ClubhouseTabPaymentJob(IMediator mediator, ILogger<ClubhouseTabPaymentJob> logger)
{
    public const string JobId = "clubhouse-tab-payment-release";
    public const string Schedule = "*/5 * * * *";

    public async Task Run()
    {
        foreach (var paymentId in await mediator.Send(new GetExpiredClubhouseTabPayments()))
        {
            try
            {
                await mediator.Send(new ReleaseExpiredClubhouseTabPayment(paymentId));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // E.g. the webhook changed the payment at the same moment; the next run looks at it again
                logger.LogWarning(e, "The payment {PaymentId} of a clubhouse tab could not be released.", paymentId);
            }
        }
    }
}
