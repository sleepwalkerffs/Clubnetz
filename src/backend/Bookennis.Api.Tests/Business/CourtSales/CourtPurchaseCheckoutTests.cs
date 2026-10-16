using Bookennis.Api.Business.CourtSales;
using Bookennis.Domain.CourtSales;
using Fusonic.Extensions.Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using SharedStatus = Bookennis.Shared.Controller.CourtSales.CourtPurchaseStatus;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary>Coming back from the payment page, leaving it, and the job that gives held slots free.</summary>
public class CourtPurchaseCheckoutTests(TestFixture fixture) : CourtSaleTestBase(fixture)
{
    [Fact]
    public async Task GetCourtPurchase_ReturnsThePurchaseWithTheCourt()
    {
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow()));

        var dto = await SendAsync(new GetCourtPurchase(TestDataSeed.ClubId, purchase.Id));

        dto.Should().BeEquivalentTo(new { purchase.Id, CourtName = "Court 1", Status = SharedStatus.Paid, PriceCents = 2000, purchase.BookingId }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Sync_CompletedSession_BooksThePayment()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment));
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        await SendAsync(new SyncCourtPurchaseCheckout(TestDataSeed.ClubId, purchase.Id));

        var paid = await Purchase(purchase.Id);
        paid.Status.Should().Be(CourtPurchaseStatus.Paid);
        paid.StripePaymentIntentId.Should().Be("pi_court");
        await Notifier.Received(1).Confirmed(Arg.Is<CourtPurchase>(p => p.Id == purchase.Id), Arg.Any<CancellationToken>());

        // A second sync finds it paid and does nothing
        await SendAsync(new SyncCourtPurchaseCheckout(TestDataSeed.ClubId, purchase.Id));
        await Notifier.Received(1).Confirmed(Arg.Any<CourtPurchase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_SessionOfSomethingElse_ChangesNothing()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment));
        StripeHasCourtSession(purchase.Id + 1, "complete", "paid");

        await SendAsync(new SyncCourtPurchaseCheckout(TestDataSeed.ClubId, purchase.Id));

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.AwaitingPayment);
    }

    [Fact]
    public async Task Sync_StripeUnreachable_IsIgnored()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment));
        Gateway.GetCheckoutSession(AccountId, "cs_test", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        await SendAsync(new SyncCourtPurchaseCheckout(TestDataSeed.ClubId, purchase.Id));

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.AwaitingPayment);
    }

    [Fact]
    public async Task Release_OpenPaymentPage_ClosesItAndGivesTheSlotFree()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment));
        StripeHasCourtSession(purchase.Id, "open", "unpaid");

        var dto = await SendAsync(new ReleaseCourtPurchase(TestDataSeed.ClubId, purchase.Id));

        dto.Status.Should().Be(SharedStatus.Expired);
        dto.BookingId.Should().BeNull();
        await Gateway.Received(1).ExpireCheckoutSession(AccountId, "cs_test", Arg.Any<CancellationToken>());
        (await BookingExists(purchase.BookingId)).Should().BeFalse();
        (await Purchase(purchase.Id)).BookingId.Should().BeNull();
    }

    [Fact]
    public async Task Release_PaidInTheMeantime_BooksThePaymentInstead()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment));
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        var dto = await SendAsync(new ReleaseCourtPurchase(TestDataSeed.ClubId, purchase.Id));

        dto.Status.Should().Be(SharedStatus.Paid);
        (await BookingExists(purchase.BookingId)).Should().BeTrue();
    }

    [Fact]
    public async Task Release_PaidPurchase_DoesNothing()
    {
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow()));

        var dto = await SendAsync(new ReleaseCourtPurchase(TestDataSeed.ClubId, purchase.Id));

        dto.Status.Should().Be(SharedStatus.Paid);
        (await BookingExists(purchase.BookingId)).Should().BeTrue();
    }

    [Fact]
    public async Task HoldJob_ReleasesExpiredHolds_ButAsksStripeFirst()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer(maxUpcomingBookings: 5);
        var expired = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 9), CourtPurchaseStatus.AwaitingPayment, sessionId: "cs_expired", holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5)));
        var paidLate = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 11), CourtPurchaseStatus.AwaitingPayment, sessionId: "cs_paid", holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5)));
        var running = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 13), CourtPurchaseStatus.AwaitingPayment, sessionId: "cs_running", holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(20)));
        StripeHasCourtSession(expired.Id, "expired", "unpaid", "cs_expired");
        StripeHasCourtSession(paidLate.Id, "complete", "paid", "cs_paid");

        await RunHoldJob();

        (await Purchase(expired.Id)).Status.Should().Be(CourtPurchaseStatus.Expired);
        (await BookingExists(expired.BookingId)).Should().BeFalse();
        (await Purchase(paidLate.Id)).Status.Should().Be(CourtPurchaseStatus.Paid);
        (await BookingExists(paidLate.BookingId)).Should().BeTrue();
        (await Purchase(running.Id)).Status.Should().Be(CourtPurchaseStatus.AwaitingPayment);
        await Gateway.DidNotReceive().GetCheckoutSession(Arg.Any<string>(), "cs_running", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HoldJob_StripeUnreachable_ReleasesTheHoldAnyway()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment, holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-10)));
        Gateway.GetCheckoutSession(AccountId, "cs_test", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        await RunHoldJob();

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Expired, "a payment that still arrives books the slot again or is reported to the treasurers");
        (await QueryAsync(ctx => ctx.Bookings.AnyAsync(b => b.Id == purchase.BookingId))).Should().BeFalse();
    }

    [Fact]
    public async Task HoldJob_OneFailingHold_DoesNotStopTheOthers()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer(maxUpcomingBookings: 5);
        var first = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 9), CourtPurchaseStatus.AwaitingPayment, sessionId: "cs_a", holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-9)));
        var second = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 11), CourtPurchaseStatus.AwaitingPayment, sessionId: "cs_b", holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5)));
        StripeHasCourtSession(first.Id, "complete", "paid", "cs_a");
        Notifier.Confirmed(Arg.Any<CourtPurchase>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("mail server down"));
        StripeHasCourtSession(second.Id, "expired", "unpaid", "cs_b");

        await RunHoldJob();

        (await Purchase(second.Id)).Status.Should().Be(CourtPurchaseStatus.Expired);
    }

    [Fact]
    public async Task GetExpiredHolds_OnlyHoldsPastTheGrace()
    {
        var (_, memberId) = await SeedBuyer(maxUpcomingBookings: 5);
        var old = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 9), CourtPurchaseStatus.AwaitingPayment, holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-3)));
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 11), CourtPurchaseStatus.AwaitingPayment, holdExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        (await SendAsync(new GetExpiredCourtPurchaseHolds())).Should().Equal(old.Id);
    }

    private Task RunHoldJob()
        => ScopedAsync(() => new CourtPurchaseHoldReleaseJob(GetInstance<IMediator>(), NullLogger<CourtPurchaseHoldReleaseJob>.Instance).Run());
}
