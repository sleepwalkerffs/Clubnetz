using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Tests.Business.Payments;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.CourtSales;
using Bookennis.Global.Intervals;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary>
/// Court sale handler tests. Stripe and the emails are substitutes; the booking domain service is a substitute that calls the real
/// rules, so the tests see what the handler passes to it and still get a real booking.
/// </summary>
public abstract class CourtSaleTestBase : PaymentTestBase
{
    protected CourtSaleTestBase(TestFixture fixture) : base(fixture)
    {
        Notifier = GetInstance<ICourtPurchaseNotifier>();
        Notifier.ClearSubstitute(ClearOptions.All);

        BookingService = GetInstance<IBookingDomainService>();
        BookingService.ClearSubstitute();
        BookingService.BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(call => new BookingDomainService().BookCourt(call.ArgAt<BookingDomainService.Context>(0), call.ArgAt<DateTimeOffsetInterval>(1), call.ArgAt<string>(2), call.ArgAt<string?>(3)));
    }

    protected ICourtPurchaseNotifier Notifier { get; }
    protected IBookingDomainService BookingService { get; }

    /// <summary>A member who is not enrolled (the club has an active season), with sales switched on and 20 €/h all day on every court.</summary>
    protected Task<(int UserId, int MemberId)> SeedBuyer(int credits = 0, int pricePerHourCents = 2000, int maxUpcomingBookings = 1) => QueryAsync(async ctx =>
    {
        await CourtSaleSeed.AddActiveSeason(ctx);
        await CourtSaleSeed.EnableSales(ctx, maxUpcomingBookings);
        await CourtSaleSeed.AddAllDayRule(ctx, pricePerHourCents);
        var buyer = await CourtSaleSeed.AddBuyer(ctx);
        if (credits != 0)
            await CourtSaleSeed.AddCredits(ctx, buyer.MemberId, credits);
        return buyer;
    });

    protected void StripeCreatesSession(string sessionId = "cs_court")
        => Gateway.CreateCheckoutSession(AccountId, Arg.Any<StripeCheckoutData>(), Arg.Any<CancellationToken>())
            .Returns(new StripeCheckoutSession(sessionId, $"https://checkout.stripe.test/{sessionId}", "open", "unpaid", null, null, null));

    protected void StripeHasCourtSession(int purchaseId, string status, string paymentStatus, string sessionId = "cs_test", string accountId = AccountId)
        => Gateway.GetCheckoutSession(accountId, sessionId, Arg.Any<CancellationToken>())
            .Returns(new StripeCheckoutSession(sessionId, null, status, paymentStatus, StripeCheckoutReference.For(StripeCheckoutKind.CourtPurchase, purchaseId), TestDataSeed.ClubId.ToString(), "pi_court"));

    protected Task<CourtPurchase> Purchase(int purchaseId) => QueryAsync(ctx => ctx.CourtPurchases.SingleAsync(p => p.Id == purchaseId));

    protected Task<bool> BookingExists(int? bookingId) => QueryAsync(ctx => ctx.Bookings.AnyAsync(b => b.Id == bookingId));
}
