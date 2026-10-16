using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.CourtSales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtSales;

public class CourtSaleOptionsAndQuoteTests(TestFixture fixture) : CourtSaleTestBase(fixture)
{
    [Fact]
    public async Task Options_SalesOff_NotEnabled()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        var options = await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId));

        options.IsEnabled.Should().BeFalse();
        options.CanBuy.Should().BeFalse();
        options.Reason.Should().Be(CourtSaleUnavailableReason.NotEnabled);
        options.TimeZoneId.Should().Be("Europe/Vienna");
    }

    [Fact]
    public async Task Options_BuyerWithCredits_CanBuy()
    {
        var (userId, _) = await SeedBuyer(credits: 2);

        var options = await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId));

        options.Should().BeEquivalentTo(new
        {
            IsEnabled = true,
            CanBuy = true,
            Reason = (CourtSaleUnavailableReason?)null,
            Credits = 2,
            MaxBookingMinutes = 120,
            MaxDaysInAdvance = 14,
            MaxUpcomingBookings = 1,
            UpcomingPurchases = 0,
            Currency = "eur",
            MinutesCoveredByCredit = 60,
        }, o => o.ExcludingMissingMembers());
        options.PlayModeId.Should().NotBeNull();
    }

    [Fact]
    public async Task Options_NoCreditAndNoStripe_PaymentUnavailable()
    {
        var (userId, _) = await SeedBuyer();

        var options = await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId));

        options.CanBuy.Should().BeFalse();
        options.Reason.Should().Be(CourtSaleUnavailableReason.PaymentUnavailable);

        await SeedAccount();
        (await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId))).CanBuy.Should().BeTrue();
    }

    [Fact]
    public async Task Options_EnrolledMember_AlreadyEnrolled_AndLimitReached()
    {
        var (userId, memberId) = await SeedBuyer(credits: 1);
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60)));

        (await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId))).Reason.Should().Be(CourtSaleUnavailableReason.LimitReached);

        await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.Enroll(ctx, memberId, await ctx.Seasons.Select(s => s.Id).SingleAsync());
            return 0;
        });
        (await SendAsync(new GetCourtSaleOptions(TestDataSeed.ClubId, userId))).Reason.Should().Be(CourtSaleUnavailableReason.AlreadyEnrolled);
    }

    [Fact]
    public async Task Quote_SlotOfTwoHoursWithACredit_TheCreditPaysTheFirstHour()
    {
        var (userId, _) = await SeedBuyer(credits: 1, pricePerHourCents: 2800);
        var slot = CourtSaleSeed.Tomorrow(120);

        var quote = await SendAsync(new GetCourtPurchaseQuote(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, slot.From, slot.To));

        quote.IsForSale.Should().BeTrue();
        quote.PriceCents.Should().Be(5600);
        quote.CreditUsed.Should().BeTrue();
        quote.AmountDueCents.Should().Be(2800);
        quote.Lines.Should().ContainSingle().Which.PricePerHourCents.Should().Be(2800);
    }

    [Fact]
    public async Task Quote_WithoutCredit_EverythingIsPaidOnline()
    {
        var (userId, _) = await SeedBuyer();
        var slot = CourtSaleSeed.Tomorrow(90);

        var quote = await SendAsync(new GetCourtPurchaseQuote(TestDataSeed.ClubId, userId, TestDataSeed.Court2Id, slot.From, slot.To));

        quote.Should().BeEquivalentTo(new { IsForSale = true, PriceCents = 3000, CreditUsed = false, AmountDueCents = 3000, Currency = "eur" }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Quote_ReasonsWhyASlotCantBeBought()
    {
        var (userId, _) = await SeedBuyer();
        var slot = CourtSaleSeed.Tomorrow(60);

        async Task<CourtSaleQuoteUnavailableReason?> Reason(DateTimeOffset from, DateTimeOffset to)
        {
            var quote = await SendAsync(new GetCourtPurchaseQuote(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, from, to));
            quote.IsForSale.Should().BeFalse();
            quote.Lines.Should().BeEmpty();
            return quote.Reason;
        }

        (await Reason(slot.From, slot.From.AddMinutes(45))).Should().Be(CourtSaleQuoteUnavailableReason.InvalidDuration);
        (await Reason(slot.From.AddMinutes(10), slot.To.AddMinutes(10))).Should().Be(CourtSaleQuoteUnavailableReason.InvalidDuration);
        (await Reason(slot.From, slot.From.AddMinutes(180))).Should().Be(CourtSaleQuoteUnavailableReason.InvalidDuration);
        (await Reason(slot.From.AddDays(30), slot.To.AddDays(30))).Should().Be(CourtSaleQuoteUnavailableReason.TooFarInAdvance);
        (await Reason(slot.From.AddDays(-3), slot.To.AddDays(-3))).Should().Be(CourtSaleQuoteUnavailableReason.InThePast);
    }

    [Fact]
    public async Task Quote_CourtOfAnotherClub_NotFound()
    {
        var (userId, _) = await SeedBuyer();
        var slot = CourtSaleSeed.Tomorrow(60);

        var act = () => SendAsync(new GetCourtPurchaseQuote(TestDataSeed.ClubId, userId, 987654, slot.From, slot.To));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }

    [Fact]
    public async Task Quote_GapInThePrices_NotForSale()
    {
        var userId = await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.EnableSales(ctx);
            ctx.Add(new Domain.CourtSales.CourtPriceRule(TestDataSeed.ClubId, new Domain.CourtSales.CourtPriceRule.RuleData([], Enum.GetValues<DayOfWeek>(), new TimeOnly(8, 0), new TimeOnly(10, 30), 2000, null)));
            await ctx.SaveChangesAsync();
            return (await CourtSaleSeed.AddBuyer(ctx)).UserId;
        });
        var slot = CourtSaleSeed.Tomorrow(60); // 10:00-11:00 local, the rule ends at 10:30

        var quote = await SendAsync(new GetCourtPurchaseQuote(TestDataSeed.ClubId, userId, TestDataSeed.Court1Id, slot.From, slot.To));

        quote.Reason.Should().Be(CourtSaleQuoteUnavailableReason.NotForSale);
    }
}
