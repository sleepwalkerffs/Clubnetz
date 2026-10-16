using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using SharedStatus = Bookennis.Shared.Controller.CourtSales.CourtPurchaseStatus;

namespace Bookennis.Api.Tests.Business.CourtSales;

public class StartCourtPurchaseTests(TestFixture fixture) : CourtSaleTestBase(fixture)
{
    [Fact]
    public async Task OneHourWithACredit_IsBookedAndPaidRightAway()
    {
        var (userId, memberId) = await SeedBuyer(credits: 2);
        var slot = CourtSaleSeed.Tomorrow(60);

        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, slot));

        result.Status.Should().Be(SharedStatus.Paid);
        result.CheckoutUrl.Should().BeNull();

        var purchase = await Purchase(result.PurchaseId);
        purchase.Should().BeEquivalentTo(new { MemberId = (int?)memberId, CreditUsed = true, AmountDueCents = 0, PriceCents = 2000, PaymentMethod = (CourtPurchasePaymentMethod?)CourtPurchasePaymentMethod.Credit }, o => o.ExcludingMissingMembers());

        var booking = await QueryAsync(ctx => ctx.Bookings.Include(b => b.Players).SingleAsync(b => b.Id == purchase.BookingId));
        booking.Players.Select(p => p.MemberId).Should().Equal(memberId);
        booking.Interval.Should().Be(slot);
        booking.CourtId.Should().Be(TestDataSeed.Court1Id);

        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, CancellationToken.None))).Should().Be(1);
        await Notifier.Received(1).Confirmed(Arg.Is<CourtPurchase>(p => p.Id == result.PurchaseId), Arg.Any<CancellationToken>());
        await Gateway.DidNotReceiveWithAnyArgs().CreateCheckoutSession(default!, default!, default);
    }

    [Fact]
    public async Task TheBookingIsValidatedAsAPurchaseWithThePlayModeOfTheSettings()
    {
        var (userId, memberId) = await SeedBuyer(credits: 1);
        var playModeId = await QueryAsync(ctx => ctx.CourtSaleSettings.Select(s => s.PlayModeId!.Value).SingleAsync());

        await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        BookingService.Received(1).BookCourt(
            Arg.Is<BookingDomainService.Context>(c => c.IsPurchase && c.PlayMode.Id == playModeId && c.Participants.Single().Member.Id == memberId),
            Arg.Any<DateTimeOffsetInterval>(),
            CourtSaleSeed.TimeZone,
            null);
    }

    [Fact]
    public async Task FreeRule_IsBookedForFree()
    {
        var (userId, _) = await SeedBuyer(pricePerHourCents: 0);

        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(90)));

        var purchase = await Purchase(result.PurchaseId);
        purchase.Status.Should().Be(CourtPurchaseStatus.Paid);
        purchase.PaymentMethod.Should().Be(CourtPurchasePaymentMethod.Free);
        purchase.CreditUsed.Should().BeFalse();
    }

    [Fact]
    public async Task FreeRuleWithCredits_KeepsTheCredit()
    {
        var (userId, memberId) = await SeedBuyer(credits: 2, pricePerHourCents: 0);

        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        var purchase = await Purchase(result.PurchaseId);
        purchase.PaymentMethod.Should().Be(CourtPurchasePaymentMethod.Free);
        purchase.CreditUsed.Should().BeFalse();
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, CancellationToken.None))).Should().Be(2);
    }

    [Fact]
    public async Task FreeFirstHourThenAPaidHour_WithCredits_KeepsTheCreditAndPaysOnline()
    {
        await SeedAccount();
        var (userId, memberId) = await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.AddActiveSeason(ctx);
            await CourtSaleSeed.EnableSales(ctx);
            var days = Enum.GetValues<DayOfWeek>();
            ctx.Add(new CourtPriceRule(TestDataSeed.ClubId, new CourtPriceRule.RuleData([], days, new TimeOnly(9, 0), new TimeOnly(11, 0), 0, "Free")));
            ctx.Add(new CourtPriceRule(TestDataSeed.ClubId, new CourtPriceRule.RuleData([], days, new TimeOnly(11, 0), new TimeOnly(13, 0), 2000, "Paid")));
            await ctx.SaveChangesAsync();
            var buyer = await CourtSaleSeed.AddBuyer(ctx);
            await CourtSaleSeed.AddCredits(ctx, buyer.MemberId, 1);
            return buyer;
        });
        StripeCreatesSession();
        var slot = CourtSaleSeed.Tomorrow(120); // 10:00-12:00: the first hour is free

        var quote = await SendAsync(new GetCourtPurchaseQuote(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, slot.From, slot.To));
        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, slot));

        quote.Should().BeEquivalentTo(new { PriceCents = 2000, CreditUsed = false, AmountDueCents = 2000 }, o => o.ExcludingMissingMembers());
        var purchase = await Purchase(result.PurchaseId);
        purchase.CreditUsed.Should().BeFalse();
        purchase.AmountDueCents.Should().Be(2000);
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, CancellationToken.None))).Should().Be(1);
    }

    [Fact]
    public async Task WithoutCredit_HoldsTheSlotAndSendsTheBuyerToStripe()
    {
        await SeedAccount();
        var (userId, _) = await SeedBuyer();
        StripeCheckoutData? sent = null;
        Gateway.CreateCheckoutSession(AccountId, Arg.Do<StripeCheckoutData>(d => sent = d), Arg.Any<CancellationToken>())
            .Returns(new StripeCheckoutSession("cs_court", "https://checkout.stripe.test/cs_court", "open", "unpaid", null, null, null));
        var slot = CourtSaleSeed.Tomorrow(90);

        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, slot));

        result.Status.Should().Be(SharedStatus.AwaitingPayment);
        result.CheckoutUrl.Should().Be("https://checkout.stripe.test/cs_court");

        var purchase = await Purchase(result.PurchaseId);
        purchase.Status.Should().Be(CourtPurchaseStatus.AwaitingPayment);
        purchase.AmountDueCents.Should().Be(3000);
        purchase.StripeCheckoutSessionId.Should().Be("cs_court");
        purchase.HoldExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.Add(StartCourtPurchase.HoldDuration), TimeSpan.FromMinutes(1));
        (await BookingExists(purchase.BookingId)).Should().BeTrue("the slot is held while the buyer pays");

        sent!.Kind.Should().Be(StripeCheckoutKind.CourtPurchase);
        sent.OrderId.Should().Be(result.PurchaseId);
        sent.AmountCents.Should().Be(3000);
        sent.ExpiresAt.Should().BeCloseTo(purchase.HoldExpiresAt!.Value, TimeSpan.FromMilliseconds(1));
        sent.ProductName.Should().StartWith("Court 1 · ");
        sent.SuccessUrl.Should().Contain($"/clubs/{TestDataSeed.ClubId}/booking?purchase={result.PurchaseId}&date=").And.EndWith("&checkout=success");
        sent.CancelUrl.Should().EndWith("&checkout=cancelled");
        await Notifier.DidNotReceiveWithAnyArgs().Confirmed(default!, default);
    }

    [Fact]
    public async Task LongerThanAnHourWithACredit_TheCreditPaysTheFirstHourAndTheRestIsPaidOnline()
    {
        await SeedAccount();
        var (userId, memberId) = await SeedBuyer(credits: 1, pricePerHourCents: 2400);
        StripeCreatesSession();

        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(120)));

        var purchase = await Purchase(result.PurchaseId);
        purchase.PriceCents.Should().Be(4800);
        purchase.CreditUsed.Should().BeTrue();
        purchase.AmountDueCents.Should().Be(2400);
        result.CheckoutUrl.Should().NotBeNull();
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, CancellationToken.None))).Should().Be(0, "the hold keeps the credit");
    }

    [Fact]
    public async Task OnlinePaymentNotPossible_NothingIsHeld()
    {
        var (userId, _) = await SeedBuyer();
        Gateway.IsConfigured.Returns(false);

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PaymentErrorCode.PaymentsNotConfigured));

        Gateway.IsConfigured.Returns(true);
        var notReady = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));
        (await notReady.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PaymentErrorCode.PaymentAccountNotReady));

        (await QueryAsync(ctx => ctx.CourtPurchases.AnyAsync())).Should().BeFalse();
        (await QueryAsync(ctx => ctx.Bookings.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task EnrolledMember_BooksForFreeInstead()
    {
        var (userId, memberId) = await SeedBuyer(credits: 1);
        await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.Enroll(ctx, memberId, await ctx.Seasons.Select(s => s.Id).SingleAsync());
            return 0;
        });

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.AlreadyEnrolled));
    }

    [Fact]
    public async Task LimitOfUpcomingBoughtBookings_IsEnforced()
    {
        var (userId, memberId) = await SeedBuyer(credits: 3);
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, localHour: 14)));

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.CourtPurchaseLimitReached));
        exception.ErrorDetails.Should().Equal("1");
    }

    [Fact]
    public async Task ExpiredOrCancelledPurchases_DoNotCountTowardTheLimit()
    {
        var (userId, memberId) = await SeedBuyer(credits: 3);
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, localHour: 14), CourtPurchaseStatus.Expired));
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, localHour: 16), CourtPurchaseStatus.Cancelled, withBooking: false));

        var result = await SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        result.Status.Should().Be(SharedStatus.Paid);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(150)]
    public async Task InvalidLength_IsRejected(int minutes)
    {
        var (userId, _) = await SeedBuyer(credits: 1);

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(minutes)));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.CourtPurchaseDurationInvalid));
    }

    [Fact]
    public async Task TooFarAhead_IsRejected()
    {
        var (userId, _) = await SeedBuyer(credits: 1);
        var slot = CourtSaleSeed.Tomorrow(60);
        var later = new DateTimeOffsetInterval(slot.From.AddDays(20), slot.To.AddDays(20));

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, later));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.CourtPurchaseTooFarInAdvance));
    }

    [Fact]
    public async Task TimeWithoutPrice_IsNotForSale()
    {
        var (userId, _) = await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.AddActiveSeason(ctx);
            await CourtSaleSeed.EnableSales(ctx);
            ctx.Add(new CourtPriceRule(TestDataSeed.ClubId, new CourtPriceRule.RuleData([TestDataSeed.Court2Id], Enum.GetValues<DayOfWeek>(), TimeOnly.MinValue, TimeOnly.MinValue, 2000, null)));
            await ctx.SaveChangesAsync();
            return await CourtSaleSeed.AddBuyer(ctx);
        });

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.CourtNotForSale));
    }

    [Fact]
    public async Task SalesSwitchedOff_IsRejected()
    {
        var (userId, _) = await SeedBuyer(credits: 1);
        await QueryAsync(async ctx =>
        {
            var settings = await ctx.CourtSaleSettings.SingleAsync();
            settings.Update(new CourtSaleSettings.SettingsData(false, settings.PlayModeId, settings.TimeZoneId, 120, 14, 1));
            await ctx.SaveChangesAsync();
            return 0;
        });

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.CourtSalesNotEnabled));
    }

    [Fact]
    public async Task TakenSlot_IsRejectedByTheBookingRules()
    {
        var (userId, _) = await SeedBuyer(credits: 2);
        var slot = CourtSaleSeed.Tomorrow(60);
        await QueryAsync(ctx => MemberSeedBooking(ctx, slot));

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, slot));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));
        (await QueryAsync(ctx => ctx.CourtPurchases.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task UserWithoutMember_CantBuy()
    {
        await SeedBuyer(credits: 1);

        var act = () => SendAsync(new StartCourtPurchase(TestDataSeed.ClubId, 987654, TestDataSeed.Court1Id, CourtSaleSeed.Tomorrow(60)));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.CourtPurchaseRequiresMember));
    }

    private static Task<int> MemberSeedBooking(Data.AppDbContext ctx, DateTimeOffsetInterval slot)
        => TestUtils.MemberSeed.AddBooking(ctx, slot.From, TestDataSeed.Member2Id);
}
