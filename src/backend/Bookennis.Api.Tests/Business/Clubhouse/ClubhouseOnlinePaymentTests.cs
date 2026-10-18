using Bookennis.Api.Business.Clubhouse;
using Bookennis.Api.Business.Clubhouse.SumUp;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Clubhouse;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;
using Xunit;
using ClubhousePaymentMethod = Bookennis.Domain.Clubhouse.ClubhousePaymentMethod;
using ClubhouseTabPaymentStatus = Bookennis.Domain.Clubhouse.ClubhouseTabPaymentStatus;
using ClubhouseTabStatus = Bookennis.Domain.Clubhouse.ClubhouseTabStatus;
using SharedPaymentStatus = Bookennis.Shared.Controller.Clubhouse.ClubhouseTabPaymentStatus;
using SharedStatus = Bookennis.Shared.Controller.Clubhouse.ClubhouseTabStatus;

namespace Bookennis.Api.Tests.Business.Clubhouse;

/// <summary>
/// Paying a tab in the app with SumUp: starting the payment (the tab is locked), the answer of SumUp from every way it arrives (member
/// coming back, webhook, the job for pages that ran out), and the tokens of the host's account. SumUp itself is a substitute.
/// </summary>
public class ClubhouseOnlinePaymentTests : TestBase
{
    private const int HostUserId = TestDataSeed.AdminId;
    private const int MemberUserId = TestDataSeed.UserId;
    private const string MerchantCode = "MTEST123";
    private const string AccessToken = "access-token";
    private const string CheckoutId = "chk_1";
    private const string CheckoutUrl = "https://checkout.sumup.test/pay/chk_1";

    private readonly ISumUpGateway gateway;
    private readonly IClubhouseTabPaymentNotifier notifier;
    private readonly SumUpTokenProtector protector;

    public ClubhouseOnlinePaymentTests(TestFixture fixture) : base(fixture)
    {
        gateway = GetInstance<ISumUpGateway>();
        gateway.ClearSubstitute(ClearOptions.All);
        gateway.IsConfigured.Returns(true);
        notifier = GetInstance<IClubhouseTabPaymentNotifier>();
        notifier.ClearSubstitute(ClearOptions.All);
        protector = GetInstance<SumUpTokenProtector>();
    }

    // ---------- helpers ----------

    private Task<int> ConnectAccount(TimeSpan? tokenValidFor = null, bool needsReconnect = false) => QueryAsync(async ctx =>
    {
        var tokens = protector.Protect(new SumUpTokens(AccessToken, DateTimeOffset.UtcNow + (tokenValidFor ?? TimeSpan.FromHours(1)), "refresh-token", "payments"));
        var account = new ClubhouseSumUpAccount(TestDataSeed.ClubId, MerchantCode, "Bar Huber", TestDataSeed.Member2Id, tokens, DateTimeOffset.UtcNow);
        if (needsReconnect)
            account.MarkNeedsReconnect();
        ctx.ClubhouseSumUpAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account.Id;
    });

    /// <summary>An open tab of the plain member with two beers (9 €).</summary>
    private async Task<ClubhouseTabDto> TabWithBeers(int? memberId = TestDataSeed.Member1Id, string? guestName = null)
    {
        var beer = await SendAsync(new CreateClubhouseItem(TestDataSeed.ClubId, new SaveClubhouseItemModel { Name = "Beer", PriceCents = 450 }));
        var tab = await SendAsync(new OpenClubhouseTab(TestDataSeed.ClubId, HostUserId, memberId, guestName));
        return await SendAsync(new AddClubhouseTabItems(TestDataSeed.ClubId, HostUserId, tab.Id, [new ClubhouseTabItemModel { ItemId = beer.Id, Quantity = 2 }]));
    }

    private void SumUpCreatesCheckout(string checkoutId = CheckoutId)
        => gateway.CreateCheckout(AccessToken, Arg.Any<SumUpCheckoutData>(), Arg.Any<CancellationToken>())
            .Returns(call => new SumUpCheckout(checkoutId, call.ArgAt<SumUpCheckoutData>(1).Reference, MerchantCode, call.ArgAt<SumUpCheckoutData>(1).AmountCents, "EUR", "PENDING", CheckoutUrl, null));

    private Task<ClubhouseRedirectResult> StartPayment(int tabId)
        => SendAsync(new StartClubhouseTabPayment(TestDataSeed.ClubId, MemberUserId, tabId));

    private async Task<ClubhouseTabPayment> StartedPayment(int tabId)
    {
        SumUpCreatesCheckout();
        await StartPayment(tabId);
        return await QueryAsync(ctx => ctx.ClubhouseTabPayments.SingleAsync(p => p.TabId == tabId && p.Status == ClubhouseTabPaymentStatus.Pending));
    }

    /// <summary>SumUp answers for the checkout of the payment with this status (and the payment's own reference and amount).</summary>
    private void SumUpHas(ClubhouseTabPayment payment, string status, int? amountCents = null, string? reference = null)
        => gateway.GetCheckout(AccessToken, CheckoutId, Arg.Any<CancellationToken>())
            .Returns(new SumUpCheckout(CheckoutId, reference ?? payment.CheckoutReference, MerchantCode, amountCents ?? payment.AmountCents, "EUR", status, CheckoutUrl, status == "PAID" ? "TX123" : null));

    private Task<ClubhouseTab> Tab(int tabId) => QueryAsync(ctx => ctx.ClubhouseTabs.Include(t => t.Lines).SingleAsync(t => t.Id == tabId));
    private Task<ClubhouseTabPayment> Payment(int paymentId) => QueryAsync(ctx => ctx.ClubhouseTabPayments.SingleAsync(p => p.Id == paymentId));

    private Task ExpirePage(int paymentId) => QueryAsync(ctx => ctx.Database.ExecuteSqlAsync(
        $"UPDATE \"ClubhouseTabPayments\" SET \"ExpiresAt\" = {DateTimeOffset.UtcNow.AddMinutes(-5)} WHERE \"Id\" = {paymentId}"));

    private static async Task Rejected(Func<Task> act, ClubhouseErrorCode code)
        => (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(code.ToString());

    // ---------- starting ----------

    [Fact]
    public async Task Start_LocksTheTabAndCreatesACheckoutForItsAmountOnTheHostsAccount()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        SumUpCreatesCheckout();

        var result = await StartPayment(tab.Id);

        result.Url.Should().Be(CheckoutUrl);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.AwaitingPayment);

        var payment = await QueryAsync(ctx => ctx.ClubhouseTabPayments.SingleAsync(p => p.TabId == tab.Id));
        payment.Should().BeEquivalentTo(new
        {
            Status = ClubhouseTabPaymentStatus.Pending,
            AmountCents = 900,
            MerchantCode,
            CheckoutId,
            CheckoutUrl,
            PayerMemberId = (int?)TestDataSeed.Member1Id,
        }, o => o.ExcludingMissingMembers());
        payment.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow + ClubhouseTabPayment.Lifetime, TimeSpan.FromMinutes(1));

        await gateway.Received(1).CreateCheckout(AccessToken, Arg.Is<SumUpCheckoutData>(d =>
            d.MerchantCode == MerchantCode
            && d.AmountCents == 900
            && d.Currency == "eur"
            && d.Reference == payment.CheckoutReference
            && d.ReturnUrl.EndsWith("/api/SumUp/Webhook")
            && d.RedirectUrl.EndsWith($"/clubs/{TestDataSeed.ClubId}/my-club?clubhouseTab={tab.Id}&clubhousePayment={payment.Id}")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_Again_WhileThePageIsOpen_LeadsToTheSamePage()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        SumUpCreatesCheckout();

        await StartPayment(tab.Id);
        var again = await StartPayment(tab.Id);

        again.Url.Should().Be(CheckoutUrl);
        await gateway.Received(1).CreateCheckout(Arg.Any<string>(), Arg.Any<SumUpCheckoutData>(), Arg.Any<CancellationToken>());
        (await QueryAsync(ctx => ctx.ClubhouseTabPayments.CountAsync(p => p.TabId == tab.Id))).Should().Be(1);
    }

    [Fact]
    public async Task Start_WithoutAConnectedAccount_IsRejected()
    {
        var tab = await TabWithBeers();

        await Rejected(() => StartPayment(tab.Id), ClubhouseErrorCode.ClubhouseOnlinePaymentNotAvailable);

        await ConnectAccount(needsReconnect: true);
        await Rejected(() => StartPayment(tab.Id), ClubhouseErrorCode.ClubhouseOnlinePaymentNotAvailable);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
    }

    [Fact]
    public async Task Start_TabOfAGuestFromOutside_OrAnEmptyTab_IsRejected()
    {
        await ConnectAccount();
        var walkIn = await TabWithBeers(memberId: null, guestName: "Table 3");
        var empty = await SendAsync(new OpenClubhouseTab(TestDataSeed.ClubId, HostUserId, TestDataSeed.Member2Id, null));

        await Rejected(() => StartPayment(walkIn.Id), ClubhouseErrorCode.ClubhouseTabNotPayableOnline);
        await Rejected(() => StartPayment(empty.Id), ClubhouseErrorCode.ClubhouseTabEmpty);
    }

    [Fact]
    public async Task Start_SumUpFails_TheTabStaysOpen()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        gateway.CreateCheckout(Arg.Any<string>(), Arg.Any<SumUpCheckoutData>(), Arg.Any<CancellationToken>()).ThrowsAsync(new SumUpException("down"));

        await Rejected(() => StartPayment(tab.Id), ClubhouseErrorCode.ClubhouseSumUpUnavailable);

        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
        (await QueryAsync(ctx => ctx.ClubhouseTabPayments.AnyAsync(p => p.Status == ClubhouseTabPaymentStatus.Pending))).Should().BeFalse();
    }

    [Fact]
    public async Task LockedTab_TheHostCanNeitherBookNorTakePayment_AndSeesItOnTheBoard()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        await StartedPayment(tab.Id);
        var itemId = tab.Lines[0].ItemId!.Value;

        await Rejected(() => SendAsync(new AddClubhouseTabItems(TestDataSeed.ClubId, HostUserId, tab.Id, [new ClubhouseTabItemModel { ItemId = itemId }])), ClubhouseErrorCode.ClubhouseTabPaymentRunning);
        await Rejected(() => SendAsync(new RemoveClubhouseTabLine(TestDataSeed.ClubId, tab.Id, tab.Lines[0].Id)), ClubhouseErrorCode.ClubhouseTabPaymentRunning);
        await Rejected(() => SendAsync(new PayClubhouseTab(TestDataSeed.ClubId, HostUserId, tab.Id, ClubhousePaymentMethod.Cash)), ClubhouseErrorCode.ClubhouseTabPaymentRunning);

        // Opening the member again returns the locked tab instead of a second one
        (await SendAsync(new OpenClubhouseTab(TestDataSeed.ClubId, HostUserId, TestDataSeed.Member1Id, null))).Id.Should().Be(tab.Id);

        var board = await SendAsync(new GetClubhouseBoard(TestDataSeed.ClubId, DateTimeOffset.UtcNow));
        board.OpenTabs.Single(t => t.Id == tab.Id).IsAwaitingPayment.Should().BeTrue();

        var mine = await SendAsync(new GetMyClubhouseTabs(TestDataSeed.ClubId, MemberUserId));
        mine.Open.Single().Status.Should().Be(SharedStatus.AwaitingPayment);
        mine.CanPayOnline.Should().BeTrue();
    }

    [Fact]
    public async Task Host_CannotBookAnOnlinePaymentByHand()
    {
        var tab = await TabWithBeers();

        await FluentActions.Awaiting(() => SendAsync(new PayClubhouseTab(TestDataSeed.ClubId, HostUserId, tab.Id, ClubhousePaymentMethod.Online)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
    }

    // ---------- the answer of SumUp ----------

    [Fact]
    public async Task MemberComesBack_Paid_TheTabIsPaidOnline_Once()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "PAID");

        var dto = await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));

        dto.Status.Should().Be(SharedPaymentStatus.Paid);
        var paidTab = await Tab(tab.Id);
        paidTab.Should().BeEquivalentTo(new { Status = ClubhouseTabStatus.Paid, PaymentMethod = (ClubhousePaymentMethod?)ClubhousePaymentMethod.Online, PaidToMemberId = (int?)null }, o => o.ExcludingMissingMembers());
        (await Payment(payment.Id)).TransactionCode.Should().Be("TX123");
        await notifier.Received(1).Paid(Arg.Is<ClubhouseTabPayment>(p => p.Id == payment.Id), Arg.Any<CancellationToken>());

        // The webhook arrives afterwards: nothing happens a second time
        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, CheckoutId));
        await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));
        await notifier.Received(1).Paid(Arg.Any<ClubhouseTabPayment>(), Arg.Any<CancellationToken>());

        // The day of the host counts it as online
        var board = await SendAsync(new GetClubhouseBoard(TestDataSeed.ClubId, DateTimeOffset.UtcNow));
        board.Today.OnlineCents.Should().Be(900);
    }

    [Fact]
    public async Task MemberComesBack_StillPending_NothingChanges()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "FAILED");

        var dto = await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));

        // FAILED is only the last attempt: the member can try again on the same page
        dto.Status.Should().Be(SharedPaymentStatus.Pending);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.AwaitingPayment);
        await gateway.DidNotReceive().DeactivateCheckout(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(100, null)]
    [InlineData(null, "clubhouse_tab:1:somebody-else")]
    public async Task CheckoutThatIsNotThePayment_ChangesNothing(int? amountCents, string? reference)
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "PAID", amountCents, reference);

        await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));

        (await Payment(payment.Id)).Status.Should().Be(ClubhouseTabPaymentStatus.Pending);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.AwaitingPayment);
        await notifier.DidNotReceiveWithAnyArgs().Paid(default!, default);
    }

    [Fact]
    public async Task SumUpUnreachable_NothingChanges()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        gateway.GetCheckout(AccessToken, CheckoutId, Arg.Any<CancellationToken>()).ThrowsAsync(new SumUpException("down"));

        var dto = await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));

        dto.Status.Should().Be(SharedPaymentStatus.Pending);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.AwaitingPayment);
    }

    [Fact]
    public async Task GetPayment_OfAnotherTabOrClub_NotFound()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);

        await FluentActions.Awaiting(() => SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id + 1, payment.Id)))
            .Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
        await FluentActions.Awaiting(() => SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId + 1, tab.Id, payment.Id)))
            .Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }

    [Fact]
    public async Task Webhook_KnownCheckout_IsCheckedWithSumUp_UnknownOrOtherEvents_AreIgnored()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "PAID");

        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, "chk_unknown"));
        await SendAsync(new ReceiveSumUpWebhook("SOMETHING_NEW", CheckoutId));
        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, null));
        await gateway.DidNotReceiveWithAnyArgs().GetCheckout(default!, default!, default);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.AwaitingPayment);

        // In tests out-of-band commands run at once
        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, CheckoutId));

        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Paid);
    }

    [Fact]
    public async Task Webhook_Expired_UnlocksTheTab()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "EXPIRED");

        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, CheckoutId));

        (await Payment(payment.Id)).Status.Should().Be(ClubhouseTabPaymentStatus.Expired);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
    }

    // ---------- pages that ran out ----------

    [Fact]
    public async Task Job_PageRanOutUnpaid_ClosesItAtSumUpAndUnlocksTheTab()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        await ExpirePage(payment.Id);
        SumUpHas(payment, "PENDING");

        (await SendAsync(new GetExpiredClubhouseTabPayments())).Should().Equal(payment.Id);
        await SendAsync(new ReleaseExpiredClubhouseTabPayment(payment.Id));

        await gateway.Received(1).DeactivateCheckout(AccessToken, CheckoutId, Arg.Any<CancellationToken>());
        (await Payment(payment.Id)).Status.Should().Be(ClubhouseTabPaymentStatus.Expired);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
        (await SendAsync(new GetExpiredClubhouseTabPayments())).Should().BeEmpty();

        // The member can start again, with a new checkout
        SumUpCreatesCheckout("chk_2");
        (await StartPayment(tab.Id)).Url.Should().Be(CheckoutUrl);
    }

    [Fact]
    public async Task Job_PaidInTheLastMoment_BooksThePayment()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        await ExpirePage(payment.Id);
        SumUpHas(payment, "PAID");

        await SendAsync(new ReleaseExpiredClubhouseTabPayment(payment.Id));

        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Paid);
        await notifier.Received(1).Paid(Arg.Any<ClubhouseTabPayment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LatePayment_AfterTheHostChangedTheTab_LeavesTheTabOpenAndTellsTheHost()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "EXPIRED");
        await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));
        await SendAsync(new AddClubhouseTabItems(TestDataSeed.ClubId, HostUserId, tab.Id, [new ClubhouseTabItemModel { ItemId = tab.Lines[0].ItemId!.Value }]));

        // SumUp reports the money after all
        SumUpHas(payment, "PAID");
        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, CheckoutId));

        (await Payment(payment.Id)).Status.Should().Be(ClubhouseTabPaymentStatus.Paid);
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
        await notifier.Received(1).NeedsAttention(Arg.Any<ClubhouseTabPayment>(), ClubhouseTabPaymentAttentionReason.TabChanged, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LatePayment_OfATabPaidInCash_TellsTheHostItWasPaidTwice()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "EXPIRED");
        await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));
        await SendAsync(new PayClubhouseTab(TestDataSeed.ClubId, HostUserId, tab.Id, ClubhousePaymentMethod.Cash));

        SumUpHas(payment, "PAID");
        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, CheckoutId));

        (await Tab(tab.Id)).PaymentMethod.Should().Be(ClubhousePaymentMethod.Cash);
        await notifier.Received(1).NeedsAttention(Arg.Any<ClubhouseTabPayment>(), ClubhouseTabPaymentAttentionReason.PaidTwice, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LatePayment_OfAnUnchangedTab_PaysIt()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        SumUpHas(payment, "EXPIRED");
        await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);

        SumUpHas(payment, "PAID");
        await SendAsync(new ReceiveSumUpWebhook(ReceiveSumUpWebhook.CheckoutStatusChanged, CheckoutId));

        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Paid);
        await notifier.DidNotReceiveWithAnyArgs().NeedsAttention(default!, default, default);
    }

    // ---------- tokens ----------

    [Fact]
    public async Task ExpiringToken_IsRefreshedAndTheNewOneKeptEncrypted()
    {
        var accountId = await ConnectAccount(tokenValidFor: TimeSpan.FromSeconds(30));
        var tab = await TabWithBeers();
        gateway.RefreshTokens("refresh-token", Arg.Any<CancellationToken>())
            .Returns(new SumUpTokens("new-access-token", DateTimeOffset.UtcNow.AddHours(1), "new-refresh-token", null));
        gateway.CreateCheckout("new-access-token", Arg.Any<SumUpCheckoutData>(), Arg.Any<CancellationToken>())
            .Returns(call => new SumUpCheckout(CheckoutId, call.ArgAt<SumUpCheckoutData>(1).Reference, MerchantCode, 900, "EUR", "PENDING", CheckoutUrl, null));

        (await StartPayment(tab.Id)).Url.Should().Be(CheckoutUrl);

        var account = await QueryAsync(ctx => ctx.ClubhouseSumUpAccounts.SingleAsync(a => a.Id == accountId));
        account.AccessToken.Should().NotContain("new-access-token");
        protector.Unprotect(account.AccessToken).Should().Be("new-access-token");
        protector.Unprotect(account.RefreshToken).Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefusedRefreshToken_MarksTheAccount_AndTheJobUnlocksTheTabAndTellsTheHost()
    {
        var accountId = await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);
        await QueryAsync(ctx => ctx.Database.ExecuteSqlAsync(
            $"UPDATE \"ClubhouseSumUpAccounts\" SET \"AccessTokenExpiresAt\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"Id\" = {accountId}"));
        gateway.RefreshTokens(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new SumUpAuthorizationException("invalid_grant"));
        await ExpirePage(payment.Id);

        await SendAsync(new ReleaseExpiredClubhouseTabPayment(payment.Id));

        (await QueryAsync(ctx => ctx.ClubhouseSumUpAccounts.SingleAsync(a => a.Id == accountId))).NeedsReconnect.Should().BeTrue();
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
        await notifier.Received(1).NeedsAttention(Arg.Any<ClubhouseTabPayment>(), ClubhouseTabPaymentAttentionReason.CouldNotVerify, Arg.Any<CancellationToken>());
        (await SendAsync(new GetMyClubhouseTabs(TestDataSeed.ClubId, MemberUserId))).CanPayOnline.Should().BeFalse();
    }

    [Fact]
    public async Task TokenThatCantBeDecrypted_MarksTheAccount_AndTheTabStaysOpen()
    {
        var accountId = await ConnectAccount();
        await QueryAsync(ctx => ctx.Database.ExecuteSqlAsync($"UPDATE \"ClubhouseSumUpAccounts\" SET \"AccessToken\" = 'lost key ring' WHERE \"Id\" = {accountId}"));
        var tab = await TabWithBeers();

        await Rejected(() => StartPayment(tab.Id), ClubhouseErrorCode.ClubhouseOnlinePaymentNotAvailable);

        (await QueryAsync(ctx => ctx.ClubhouseSumUpAccounts.SingleAsync(a => a.Id == accountId))).NeedsReconnect.Should().BeTrue();
        (await Tab(tab.Id)).Status.Should().Be(ClubhouseTabStatus.Open);
        (await SendAsync(new GetMyClubhouseTabs(TestDataSeed.ClubId, MemberUserId))).CanPayOnline.Should().BeFalse();
    }

    // ---------- connecting the host's account ----------

    [Fact]
    public async Task Connect_TheHostSignsInAtSumUp_AndTheAccountIsStoredWithEncryptedTokens()
    {
        gateway.AuthorizeUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(call => $"https://api.sumup.test/authorize?state={call.ArgAt<string>(0)}");
        gateway.ExchangeCode("the-code", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SumUpTokens("plain-access", DateTimeOffset.UtcNow.AddHours(1), "plain-refresh", "payments"));
        gateway.GetMerchant("plain-access", Arg.Any<CancellationToken>()).Returns(new SumUpMerchant(MerchantCode, "Bar Huber"));

        var start = await SendAsync(new StartClubhouseSumUpConnect(TestDataSeed.ClubId, HostUserId));
        var state = Uri.UnescapeDataString(start.Url.Split("state=")[1]);
        gateway.Received(1).AuthorizeUrl(state, Arg.Is<string>(u => u.EndsWith("/api/SumUp/Callback")));

        var page = await SendAsync(new CompleteClubhouseSumUpConnect(HostUserId, "the-code", state, null));

        page.Should().Be($"/clubs/{TestDataSeed.ClubId}/clubhouse?view=sumup&sumup=connected");
        var account = await QueryAsync(ctx => ctx.ClubhouseSumUpAccounts.SingleAsync());
        account.Should().BeEquivalentTo(new { ClubId = TestDataSeed.ClubId, MerchantCode, MerchantName = "Bar Huber", ConnectedByMemberId = (int?)TestDataSeed.Member2Id, NeedsReconnect = false }, o => o.ExcludingMissingMembers());
        account.AccessToken.Should().NotContain("plain-access");
        account.RefreshToken.Should().NotContain("plain-refresh");

        var dto = await SendAsync(new GetClubhouseSumUpAccount(TestDataSeed.ClubId));
        dto.Should().BeEquivalentTo(new { IsConfigured = true, IsConnected = true, MerchantCode, MerchantName = "Bar Huber" }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Connect_IsOnlyCompletedByTheUserWhoStartedIt_AndOnlyForTheHostOrAnAdmin()
    {
        gateway.AuthorizeUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(call => $"https://api.sumup.test/authorize?state={call.ArgAt<string>(0)}");
        var start = await SendAsync(new StartClubhouseSumUpConnect(TestDataSeed.ClubId, HostUserId));
        var state = Uri.UnescapeDataString(start.Url.Split("state=")[1]);

        // A link of the admin opened by another user, a forged state, an error from SumUp
        (await SendAsync(new CompleteClubhouseSumUpConnect(MemberUserId, "the-code", state, null))).Should().Be("/accessdenied");
        (await SendAsync(new CompleteClubhouseSumUpConnect(HostUserId, "the-code", "forged", null))).Should().Be("/");
        (await SendAsync(new CompleteClubhouseSumUpConnect(HostUserId, null, state, "access_denied"))).Should().EndWith("sumup=failed");

        // A plain member can't even start
        await FluentActions.Awaiting(() => SendAsync(new StartClubhouseSumUpConnect(TestDataSeed.ClubId, MemberUserId)))
            .Should().ThrowAsync<Bookennis.Api.Infrastructure.Exceptions.AccessDeniedException>();

        await gateway.DidNotReceiveWithAnyArgs().ExchangeCode(default!, default!, default);
        (await QueryAsync(ctx => ctx.ClubhouseSumUpAccounts.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task Disconnect_NotWhileAMemberPays()
    {
        await ConnectAccount();
        var tab = await TabWithBeers();
        var payment = await StartedPayment(tab.Id);

        await Rejected(() => SendAsync(new DisconnectClubhouseSumUp(TestDataSeed.ClubId, HostUserId)), ClubhouseErrorCode.ClubhouseSumUpPaymentsRunning);

        SumUpHas(payment, "PAID");
        await SendAsync(new GetClubhouseTabPayment(TestDataSeed.ClubId, tab.Id, payment.Id));
        await SendAsync(new DisconnectClubhouseSumUp(TestDataSeed.ClubId, HostUserId));

        (await QueryAsync(ctx => ctx.ClubhouseSumUpAccounts.AnyAsync())).Should().BeFalse();
        (await SendAsync(new GetClubhouseSumUpAccount(TestDataSeed.ClubId))).IsConnected.Should().BeFalse();
    }
}
