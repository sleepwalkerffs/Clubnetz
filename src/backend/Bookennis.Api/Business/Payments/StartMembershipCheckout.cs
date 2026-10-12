using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>
/// Sends the member to Stripe to pay an order: creates a Checkout Session on the club's Stripe account (the club is the merchant)
/// and returns the address of the payment page. Stripe reports the result by webhook (<see cref="ProcessStripeEvent"/>),
/// the redirect back to the app proves nothing.
/// </summary>
public record StartMembershipCheckout(int ClubId, int UserId, int MembershipOrderId) : ICommand<StartMembershipCheckoutResult>
{
    public enum ErrorCode
    {
        /// <summary>A free plan is confirmed, not paid.</summary>
        MembershipOrderIsFree = 0,
    }

    public class Handler(AppDbContext context, IStripeGateway gateway, AppSettings appSettings, IUserLanguageAccessor languageAccessor)
        : IRequestHandler<StartMembershipCheckout, StartMembershipCheckoutResult>
    {
        public async Task<StartMembershipCheckoutResult> Handle(StartMembershipCheckout request, CancellationToken cancellationToken)
        {
            if (!gateway.IsConfigured)
                throw new PaymentsNotConfiguredException();

            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);
            if (order.IsFree)
                throw new PreconditionException(ErrorCode.MembershipOrderIsFree, "A free plan is confirmed instead of paid.");

            order.EnsurePayable();

            var account = await context.ClubPaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken);
            if (account is not { ChargesEnabled: true })
                throw new PreconditionException(PaymentErrorCode.PaymentAccountNotReady, "The club can't take online payments at the moment.");

            var season = await context.GetMembershipSeason(request.ClubId, order.SeasonId, cancellationToken);

            // A failed order is open again: make sure nobody of it got another membership in the meantime
            await context.LockMembershipOrders(request.ClubId, season.Id, cancellationToken);
            var people = await MembershipPeople.Load(context, request.ClubId, season, order.Members.Select(m => m.MemberId).ToList(), cancellationToken);
            if (order.Members.Any(m => people.Find(m.MemberId) is { HasMembership: true } person && person.ActiveOrderId != order.Id))
                throw new PreconditionException(MembershipEligibility.ErrorCode.AlreadyHasMembership, "A member already has a membership in this season.");

            var previousSessionId = order.StripeCheckoutSessionId;
            var culture = languageAccessor.GetCultureInfo();
            var payerEmail = await context.Users.Where(u => u.Id == request.UserId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
            var memberNames = string.Join(", ", order.Members.Select(m => people.Find(m.MemberId)).Where(p => p is not null).Select(p => $"{p!.FirstName} {p.LastName}"));

            var page = $"{appSettings.AppUri.AbsoluteUri.TrimEnd('/')}/clubs/{order.ClubId}/membership?season={order.SeasonId}&order={order.Id}";
            var session = await StripeCalls.Run(() => gateway.CreateCheckoutSession(
                account.StripeAccountId,
                new StripeCheckoutData(
                    order.ClubId,
                    order.Id,
                    $"{order.PlanName} · {ClubEmailCatalog.FormatSeason(season.Period.From, season.Period.To, culture).Period}",
                    memberNames.Length > 0 ? memberNames : null,
                    order.AmountCents,
                    order.Currency,
                    payerEmail,
                    culture.TwoLetterISOLanguageName,
                    ApplicationFee(order.AmountCents, appSettings.Stripe.ApplicationFeePercent),
                    $"{page}&checkout=success",
                    $"{page}&checkout=cancelled"),
                cancellationToken));

            if (session.Url is null)
                throw new PreconditionException(PaymentErrorCode.PaymentProviderUnavailable, "The payment page could not be created.");

            order.StartCheckout(session.Id);
            await context.SaveChangesAsync(cancellationToken);

            // Only one payment page per order, so the fee can't be paid twice
            if (previousSessionId is not null && previousSessionId != session.Id)
                await StripeCalls.Run(async () =>
                {
                    await gateway.ExpireCheckoutSession(account.StripeAccountId, previousSessionId, cancellationToken);
                    return true;
                });

            return new StartMembershipCheckoutResult(session.Url);
        }

        /// <summary>The share of the platform in cents, rounded down; <c>null</c> when the setting is 0.</summary>
        public static long? ApplicationFee(int amountCents, decimal percent)
        {
            if (percent <= 0)
                return null;

            var fee = (long)Math.Floor(amountCents * Math.Min(percent, 100m) / 100m);
            return fee > 0 ? fee : null;
        }
    }
}
