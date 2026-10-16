using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.CourtSales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using CourtPurchaseStatus = Bookennis.Domain.CourtSales.CourtPurchaseStatus;
using SharedStatus = Bookennis.Shared.Controller.CourtSales.CourtPurchaseStatus;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary>What treasurers do: settings, prices, the list of sales, cancelling and credits.</summary>
public class CourtSaleManagementTests(TestFixture fixture) : CourtSaleTestBase(fixture)
{
    // ---------- settings ----------

    [Fact]
    public async Task Settings_WithoutRow_AreTheDefaults()
    {
        var settings = await SendAsync(new GetCourtSaleSettings(TestDataSeed.ClubId));

        settings.Should().BeEquivalentTo(new { IsEnabled = false, PlayModeId = (int?)null, TimeZoneId = "Europe/Vienna", MaxBookingMinutes = 120, MaxDaysInAdvance = 14, MaxUpcomingBookings = 1, IsOnlinePaymentAvailable = false });
    }

    [Fact]
    public async Task Settings_AreSavedAndChanged()
    {
        // The seeded play mode has a fixed duration; bought slots need one without
        var playModeId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == TestDataSeed.ClubId);
            club.AddPlayMode(new Domain.Clubs.Club.PlayModeDto([Domain.Members.MemberRole.User], System.Drawing.Color.Gray, null, false, "Bought", null, false, false));
            await ctx.SaveChangesAsync();
            return club.PlayModes.Single(p => p.Name == "Bought").Id;
        });
        await SeedAccount();

        var saved = await SendAsync(new UpdateCourtSaleSettings(TestDataSeed.ClubId, new UpdateCourtSaleSettingsModel { IsEnabled = true, PlayModeId = playModeId, TimeZoneId = "Europe/Berlin", MaxBookingMinutes = 90, MaxDaysInAdvance = 7, MaxUpcomingBookings = 2 }));
        var changed = await SendAsync(new UpdateCourtSaleSettings(TestDataSeed.ClubId, new UpdateCourtSaleSettingsModel { IsEnabled = false, PlayModeId = playModeId, TimeZoneId = "Europe/Vienna", MaxBookingMinutes = 60, MaxDaysInAdvance = 0, MaxUpcomingBookings = 1 }));

        saved.Should().BeEquivalentTo(new { IsEnabled = true, PlayModeId = (int?)playModeId, TimeZoneId = "Europe/Berlin", MaxBookingMinutes = 90, IsOnlinePaymentAvailable = true }, o => o.ExcludingMissingMembers());
        changed.IsEnabled.Should().BeFalse();
        (await QueryAsync(ctx => ctx.CourtSaleSettings.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Settings_PlayModeThatOverbooksOrHasAFixedDuration_IsRejected()
    {
        var (overbooking, fixedDuration) = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == TestDataSeed.ClubId);
            club.AddPlayMode(new Domain.Clubs.Club.PlayModeDto([Domain.Members.MemberRole.Admin], System.Drawing.Color.Red, null, false, "Tournament", null, true, false));
            club.AddPlayMode(new Domain.Clubs.Club.PlayModeDto([Domain.Members.MemberRole.User], System.Drawing.Color.Blue, null, false, "Hour", TimeSpan.FromHours(1), false, false));
            await ctx.SaveChangesAsync();
            return (club.PlayModes.Single(p => p.Name == "Tournament").Id, club.PlayModes.Single(p => p.Name == "Hour").Id);
        });

        Task Save(int playModeId) => SendAsync(new UpdateCourtSaleSettings(TestDataSeed.ClubId, new UpdateCourtSaleSettingsModel { IsEnabled = true, PlayModeId = playModeId, TimeZoneId = "Europe/Vienna", MaxBookingMinutes = 60, MaxDaysInAdvance = 1, MaxUpcomingBookings = 1 }));

        (await FluentActions.Awaiting(() => Save(overbooking)).Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtSalePlayModeCanOverbook));
        (await FluentActions.Awaiting(() => Save(fixedDuration)).Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtSalePlayModeHasFixedDuration));
        (await QueryAsync(ctx => ctx.CourtSaleSettings.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task Settings_PlayModeOfAnotherClub_NotFound()
    {
        var act = () => SendAsync(new UpdateCourtSaleSettings(TestDataSeed.ClubId, new UpdateCourtSaleSettingsModel { IsEnabled = true, PlayModeId = 987654, TimeZoneId = "Europe/Vienna", MaxBookingMinutes = 60, MaxDaysInAdvance = 1, MaxUpcomingBookings = 1 }));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }

    // ---------- price rules ----------

    [Fact]
    public async Task PriceRules_AreCreatedChangedListedAndDeleted()
    {
        var created = await SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, Rule([DayOfWeek.Saturday], 8, 12, 2200, "Weekend")));
        var midnight = await SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, new SaveCourtPriceRuleModel { Weekdays = [DayOfWeek.Monday], From = TimeSpan.FromHours(20), To = TimeSpan.FromDays(1), PricePerHourCents = 1000, CourtIds = [TestDataSeed.Court2Id] }));

        midnight.To.Should().Be(TimeSpan.FromDays(1));
        midnight.CourtIds.Should().Equal(TestDataSeed.Court2Id);

        var updated = await SendAsync(new UpdateCourtPriceRule(TestDataSeed.ClubId, created.Id, Rule([DayOfWeek.Saturday, DayOfWeek.Sunday], 8, 14, 2400, "Weekend")));
        updated.Weekdays.Should().Equal(DayOfWeek.Sunday, DayOfWeek.Saturday);
        updated.To.Should().Be(TimeSpan.FromHours(14));

        var rules = await SendAsync(new GetCourtPriceRules(TestDataSeed.ClubId));
        rules.Select(r => r.Id).Should().Equal(midnight.Id, created.Id); // Monday before the weekend

        await SendAsync(new DeleteCourtPriceRule(TestDataSeed.ClubId, created.Id));
        (await SendAsync(new GetCourtPriceRules(TestDataSeed.ClubId))).Should().ContainSingle();
    }

    [Fact]
    public async Task PriceRules_Overlapping_AreRejected_ButARuleMayChangeItself()
    {
        var morning = await SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, Rule([DayOfWeek.Monday, DayOfWeek.Tuesday], 8, 12, 2000, "Morning")));

        var act = () => SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, Rule([DayOfWeek.Tuesday], 11, 13, 2400, null)));

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPriceRulesOverlap));
        exception.ErrorDetails.Should().Equal("Morning", "08:00", "12:00");

        await SendAsync(new UpdateCourtPriceRule(TestDataSeed.ClubId, morning.Id, Rule([DayOfWeek.Monday, DayOfWeek.Tuesday], 9, 12, 2000, "Morning")));
    }

    [Fact]
    public async Task PriceRules_CourtOfAnotherClub_NotFound_AndInvalidTimes_AreRejected()
    {
        var foreignCourt = () => SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, Rule([DayOfWeek.Monday], 8, 9, 1000, null) with { CourtIds = [987654] }));
        var invalid = () => SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, Rule([DayOfWeek.Monday], 8, 9, 1000, null) with { To = TimeSpan.FromHours(30) }));
        var unknown = () => SendAsync(new DeleteCourtPriceRule(TestDataSeed.ClubId, 987654));

        await foreignCourt.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
        (await invalid.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPriceRuleInvalidTime));
        await unknown.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }

    // ---------- sales ----------

    [Fact]
    public async Task Purchases_ListedWithTheNameOfTheBuyerAndTotals()
    {
        var (_, memberId) = await SeedBuyer(maxUpcomingBookings: 5);
        var paid = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 9), priceCents: 2800));
        var credit = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 11), creditUsed: true));
        var waiting = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 13), CourtPurchaseStatus.AwaitingPayment, sessionId: "cs_wait"));

        var result = await SendAsync(new GetCourtPurchases(TestDataSeed.ClubId, null, null, null));

        result.Purchases.Select(p => p.Id).Should().Equal(waiting.Id, credit.Id, paid.Id);
        result.Purchases.Should().OnlyContain(p => p.FirstName == "First" && p.LastName == "buyer" && p.CourtName == "Court 1");
        result.Summary.Should().BeEquivalentTo(new CourtPurchasesSummaryDto { SoldCount = 2, OnlineAmountCents = 2800, CreditsUsed = 1, AwaitingPaymentCount = 1, NeedsAttentionCount = 0 });

        var onlyPaid = await SendAsync(new GetCourtPurchases(TestDataSeed.ClubId, null, null, SharedStatus.Paid));
        onlyPaid.Purchases.Should().HaveCount(2);

        var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(CourtSaleSeed.Tomorrow().From, TimeZoneInfo.FindSystemTimeZoneById(CourtSaleSeed.TimeZone)).DateTime);
        (await SendAsync(new GetCourtPurchases(TestDataSeed.ClubId, tomorrow, tomorrow, null))).Purchases.Should().HaveCount(3);
        (await SendAsync(new GetCourtPurchases(TestDataSeed.ClubId, tomorrow.AddDays(1), null, null))).Purchases.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_PaidOnline_DeletesTheBookingAndTellsToRefundInStripe()
    {
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow()));
        var treasurerUserId = Query(ctx => ctx.TestData().Admin.Id);

        var result = await SendAsync(new CancelCourtPurchase(TestDataSeed.ClubId, purchase.Id, treasurerUserId));

        result.RefundInStripeDashboard.Should().BeTrue();
        result.Purchase.Status.Should().Be(SharedStatus.Cancelled);
        result.Purchase.CanCancel.Should().BeFalse();
        (await BookingExists(purchase.BookingId)).Should().BeFalse();
        (await Purchase(purchase.Id)).CancelledByMemberId.Should().Be(TestDataSeed.Member2Id);
    }

    [Fact]
    public async Task Cancel_PaidWithACredit_GivesTheCreditBack()
    {
        var (_, memberId) = await SeedBuyer(credits: 1);
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), creditUsed: true));
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, CancellationToken.None))).Should().Be(0);

        var result = await SendAsync(new CancelCourtPurchase(TestDataSeed.ClubId, purchase.Id, 987654));

        result.RefundInStripeDashboard.Should().BeFalse();
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, CancellationToken.None))).Should().Be(1);
        (await Purchase(purchase.Id)).CancelledByMemberId.Should().BeNull("support mode has no member");
    }

    [Fact]
    public async Task Cancel_Hold_ClosesThePaymentPage()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment, sessionId: "cs_open"));

        var result = await SendAsync(new CancelCourtPurchase(TestDataSeed.ClubId, purchase.Id, 987654));

        result.RefundInStripeDashboard.Should().BeFalse();
        await Gateway.Received(1).ExpireCheckoutSession(AccountId, "cs_open", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_Twice_IsRejected()
    {
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow()));
        await SendAsync(new CancelCourtPurchase(TestDataSeed.ClubId, purchase.Id, 987654));

        var act = () => SendAsync(new CancelCourtPurchase(TestDataSeed.ClubId, purchase.Id, 987654));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPurchaseInvalidStatus));
    }

    // ---------- credits ----------

    [Fact]
    public async Task Credits_AreGrantedTakenBackAndListed()
    {
        var (_, memberId) = await SeedBuyer();
        var adminUserId = Query(ctx => ctx.TestData().Admin.Id);

        var granted = await SendAsync(new GrantBookingCredits(TestDataSeed.ClubId, adminUserId, memberId, 3, "  Voucher  "));
        granted.Should().BeEquivalentTo(new { MemberId = memberId, Granted = 3, Used = 0, Balance = 3, FirstName = "First", LastName = "buyer" }, o => o.ExcludingMissingMembers());
        granted.Grants.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Count = 3, Note = "Voucher", Reason = Bookennis.Shared.Controller.CourtSales.BookingCreditReason.GrantedByClub, GrantedByName = "ad min" }, o => o.ExcludingMissingMembers());

        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), creditUsed: true));
        var corrected = await SendAsync(new GrantBookingCredits(TestDataSeed.ClubId, adminUserId, memberId, -2, null));
        corrected.Balance.Should().Be(0);
        corrected.Used.Should().Be(1);

        var tooMuch = () => SendAsync(new GrantBookingCredits(TestDataSeed.ClubId, adminUserId, memberId, -1, null));
        var exception = (await tooMuch.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.BookingCreditBalanceTooLow));
        exception.ErrorDetails.Should().Equal("0");

        var list = await SendAsync(new GetBookingCredits(TestDataSeed.ClubId));
        list.Members.Should().ContainSingle().Which.Grants.Should().HaveCount(2);
    }

    [Fact]
    public async Task Credits_ForAMemberOfAnotherClub_NotFound()
    {
        var act = () => SendAsync(new GrantBookingCredits(TestDataSeed.ClubId, TestDataSeed.AdminId, 987654, 1, null));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }

    [Fact]
    public async Task CreditCandidates_AreFoundByName()
    {
        await SeedBuyer();

        var all = await SendAsync(new GetBookingCreditCandidates(TestDataSeed.ClubId, null));
        var found = await SendAsync(new GetBookingCreditCandidates(TestDataSeed.ClubId, "buy fir"));
        var none = await SendAsync(new GetBookingCreditCandidates(TestDataSeed.ClubId, "zz%"));

        all.Should().HaveCount(3);
        found.Should().ContainSingle().Which.LastName.Should().Be("buyer");
        none.Should().BeEmpty();
    }

    private static SaveCourtPriceRuleModel Rule(List<DayOfWeek> days, int from, int to, int price, string? name) => new()
    {
        Weekdays = days,
        From = TimeSpan.FromHours(from),
        To = TimeSpan.FromHours(to),
        PricePerHourCents = price,
        Name = name,
    };
}
