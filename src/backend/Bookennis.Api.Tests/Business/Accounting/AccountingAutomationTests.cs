using Bookennis.Api.Business.Accounting;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Tests.Business.CourtSales;
using Bookennis.Api.Tests.Business.Memberships;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Accounting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using AccountingAutomationMode = Bookennis.Shared.Controller.Accounting.AccountingAutomationMode;
using AccountingEntryKind = Bookennis.Shared.Controller.Accounting.AccountingEntryKind;
using AccountingProposalStatus = Bookennis.Domain.Accounting.AccountingProposalStatus;
using AccountingSource = Bookennis.Shared.Controller.Accounting.AccountingSource;
using MoneyAccountKind = Bookennis.Shared.Controller.Accounting.MoneyAccountKind;

namespace Bookennis.Api.Tests.Business.Accounting;

/// <summary>The automatic entries: suggestions from membership fees, court sales, the clubhouse and Stripe, booked or reviewed.</summary>
public class AccountingAutomationTests(TestFixture fixture) : TestBase(fixture)
{
    private const int ClubId = TestDataSeed.ClubId;
    private const int TreasurerUserId = TestDataSeed.AdminId;
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateOnly Today = LocalDate(Now);

    private static DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(Bookennis.Domain.Clubs.ClubTimeZone.ToLocal(value));

    private sealed record Books(AccountingDto Basics, int Bank, int Cash, int Online, int Card, int Fees, int Courts, int Canteen, int Membership);

    /// <summary>The books of this year and the year before, with a Stripe balance and a card terminal account.</summary>
    private async Task<Books> SetUp()
    {
        await SendAsync(new SetUpAccounting(ClubId, new SetUpAccountingModel { FiscalYearStartMonth = 1, FirstYear = Today.Year - 1 }, Now));
        await SendAsync(new CreateFiscalYear(ClubId, Now));
        var online = await SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Stripe", Kind = MoneyAccountKind.PaymentProvider }));
        var card = await SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "SumUp", Kind = MoneyAccountKind.PaymentProvider }));
        var basics = await SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, false));
        int Category(string number) => basics.Categories.Single(c => c.Number == number).Id;
        return new Books(basics, basics.MoneyAccounts.First(a => a.Kind == MoneyAccountKind.Bank).Id, basics.MoneyAccounts.First(a => a.Kind == MoneyAccountKind.Cash).Id,
            online.Id, card.Id, Category("5710"), Category("4100"), Category("4300"), Category("4000"));
    }

    private Task<AccountingAutomationDto> Automate(Books books, AccountingSource source, AccountingAutomationMode mode, int categoryId, DateOnly? since = null)
        => SendAsync(new UpdateAccountingAutomation(ClubId, new SaveAccountingAutomationModel
        {
            OnlineMoneyAccountId = books.Online,
            BankMoneyAccountId = books.Bank,
            CashMoneyAccountId = books.Cash,
            CardMoneyAccountId = books.Card,
            Sources = [new AccountingSourceSettingDto { Source = source, Mode = mode, CategoryId = categoryId, Since = since ?? Today.AddDays(-30) }],
        }, Now));

    private Task<SyncAccountingProposalsResult> Sync() => SendAsync(new SyncAccountingProposals(ClubId, Now));

    private Task<List<AccountingProposalDto>> Open() => SendAsync(new GetAccountingProposals(ClubId, AccountingProposalStatus.Open));

    private static async Task Rejected(Func<Task> act, AccountingErrorCode code)
        => (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(code.ToString());

    private async Task<MembershipOrder> PaidOrder(MembershipPaymentMethod method, int priceCents, bool refunded = false)
        => await QueryAsync(async ctx =>
        {
            var season = await ctx.Seasons.FirstOrDefaultAsync(s => s.ClubId == ClubId) ?? await MembershipSeed.SeedSeason(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Plan " + Guid.NewGuid().ToString("N")[..6], priceCents));
            var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            order.MarkPaid(method, Now.AddDays(-2), paymentIntentId: method == MembershipPaymentMethod.Online ? "pi_" + order.Id : null);
            if (refunded)
                order.MarkRefunded();
            await ctx.SaveChangesAsync();
            return order;
        });

    [Fact]
    public async Task Automation_NeedsTheCategoryOfTheRightKindAndTheAccounts()
    {
        var books = await SetUp();

        await Rejected(() => Automate(books, AccountingSource.MembershipFees, AccountingAutomationMode.Book, books.Fees), AccountingErrorCode.AccountingAutomationIncomplete);
        await Rejected(() => SendAsync(new UpdateAccountingAutomation(ClubId, new SaveAccountingAutomationModel
        {
            Sources = [new AccountingSourceSettingDto { Source = AccountingSource.Clubhouse, Mode = AccountingAutomationMode.Review, CategoryId = books.Canteen }],
        }, Now)), AccountingErrorCode.AccountingAutomationIncomplete);

        var automation = await Automate(books, AccountingSource.Stripe, AccountingAutomationMode.Review, books.Fees);
        automation.Sources.Should().HaveCount(4);
        automation.Sources.Single(s => s.Source == AccountingSource.Stripe).Mode.Should().Be(AccountingAutomationMode.Review);
        automation.Sources.Single(s => s.Source == AccountingSource.CourtSales).Mode.Should().Be(AccountingAutomationMode.Off);
        automation.OnlineMoneyAccountId.Should().Be(books.Online);
    }

    [Fact]
    public async Task MembershipFees_AreSuggestedOnce_IntoTheAccountOfTheWayOfPaying()
    {
        var books = await SetUp();
        var bankOrder = await PaidOrder(MembershipPaymentMethod.BankTransfer, 12_000);
        await PaidOrder(MembershipPaymentMethod.Cash, 6_000);
        var refunded = await PaidOrder(MembershipPaymentMethod.Online, 9_000, refunded: true);
        await Automate(books, AccountingSource.MembershipFees, AccountingAutomationMode.Review, books.Membership);

        var result = await Sync();

        result.Created.Should().Be(4);
        result.Booked.Should().Be(0);
        var open = await Open();
        open.Select(p => (p.MoneyAccountId, p.AmountCents)).Should().BeEquivalentTo(
            [(books.Bank, 12_000), (books.Cash, 6_000), (books.Online, 9_000), (books.Online, -9_000)]);
        var bank = open.Single(p => p.AmountCents == 12_000);
        bank.Text.Should().StartWith("Mitgliedsbeitrag ");
        bank.CounterpartyMemberId.Should().Be(TestDataSeed.Member1Id);
        bank.Date.Should().Be(LocalDate(bankOrder.PaidAt!.Value));
        open.Single(p => p.AmountCents == -9_000).Text.Should().StartWith("Rückerstattung").And.Contain(refunded.PlanName);

        (await Sync()).Created.Should().Be(0, "every payment is suggested once");

        // Booked as suggested, booked as changed, dismissed: none of them comes back
        var entry = await SendAsync(new BookAccountingProposal(ClubId, TreasurerUserId, bank.Id, new BookAccountingProposalModel(), Now));
        entry.Lines.Should().ContainSingle().Which.AmountCents.Should().Be(12_000);
        entry.MoneyAccountId.Should().Be(books.Bank);

        var cash = open.Single(p => p.AmountCents == 6_000);
        var changed = await SendAsync(new BookAccountingProposal(ClubId, TreasurerUserId, cash.Id, new BookAccountingProposalModel
        {
            Entry = new SaveAccountingEntryModel
            {
                Date = cash.Date, Text = "Beitrag bar, Quittung 7", Kind = AccountingEntryKind.Booking, MoneyAccountId = books.Cash,
                Lines = [new SaveAccountingEntryLineModel { CategoryId = books.Membership, AmountCents = 6_000 }],
            },
        }, Now));
        changed.Text.Should().Be("Beitrag bar, Quittung 7");

        await SendAsync(new DismissAccountingProposal(ClubId, open.Single(p => p.AmountCents == 9_000).Id));
        await Rejected(() => SendAsync(new DismissAccountingProposal(ClubId, bank.Id)), AccountingErrorCode.AccountingProposalNotOpen);

        (await Sync()).Created.Should().Be(0);
        (await Open()).Should().ContainSingle().Which.AmountCents.Should().Be(-9_000);
        (await SendAsync(new GetAccountingProposals(ClubId, AccountingProposalStatus.Booked))).Select(p => p.EntryId).Should().Contain(entry.Id);
    }

    [Fact]
    public async Task BookingMode_BooksByItself_AndKeepsWhatHasNoOpenYear()
    {
        var books = await SetUp();
        await PaidOrder(MembershipPaymentMethod.BankTransfer, 12_000);
        await Automate(books, AccountingSource.MembershipFees, AccountingAutomationMode.Book, books.Membership);

        // An order paid before the first fiscal year in Clubnetz
        var old = await PaidOrder(MembershipPaymentMethod.Cash, 3_000);
        await QueryAsync(async ctx =>
        {
            await ctx.Database.ExecuteSqlAsync($"""UPDATE "MembershipOrders" SET "PaidAt" = {new DateTimeOffset(Today.Year - 2, 6, 1, 10, 0, 0, TimeSpan.Zero)} WHERE "Id" = {old.Id}""");
            return 0;
        });
        await Automate(books, AccountingSource.MembershipFees, AccountingAutomationMode.Book, books.Membership, since: new DateOnly(Today.Year - 2, 1, 1));

        var result = await Sync();

        result.Booked.Should().Be(1);
        result.Open.Should().Be(1);
        (await Open()).Should().ContainSingle().Which.Problem.Should().Be(nameof(AccountingErrorCode.AccountingFiscalYearMissing));
        var entries = await SendAsync(new GetAccountingEntries(ClubId, (await QueryAsync(ctx => ctx.FiscalYears.SingleAsync(y => y.ClubId == ClubId && y.StartDate.Year == Today.Year))).Id, null, null, null, null, false));
        entries.Entries.Should().ContainSingle().Which.AmountCents.Should().Be(12_000);
    }

    [Fact]
    public async Task CourtSales_PaidOnline_GoToTheStripeBalance()
    {
        var books = await SetUp();
        var (_, buyerId) = await QueryAsync(ctx => CourtSaleSeed.AddBuyer(ctx));
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, buyerId, CourtSaleSeed.Tomorrow(), priceCents: 2_500));
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, buyerId, CourtSaleSeed.Tomorrow(localHour: 14), creditUsed: true));
        await Automate(books, AccountingSource.CourtSales, AccountingAutomationMode.Review, books.Courts);

        await Sync();

        var proposal = (await Open()).Should().ContainSingle("a court paid with a credit brings no money").Subject;
        proposal.AmountCents.Should().Be(2_500);
        proposal.MoneyAccountId.Should().Be(books.Online);
        proposal.Text.Should().StartWith("Platzbuchung ");
    }

    [Fact]
    public async Task Clubhouse_IsBookedPerDayAndWayOfPaying_OnlyForDaysThatAreOver()
    {
        var books = await SetUp();
        await QueryAsync(async ctx =>
        {
            var beer = new ClubhouseItem(ClubId, new ClubhouseItem.ItemData("Bier", ClubhouseItemCategory.Drink, 400, true, 0));
            ctx.Add(beer);
            await ctx.SaveChangesAsync();

            var yesterday = Now.AddDays(-1);
            foreach (var (method, quantity, paidAt) in new[] { (ClubhousePaymentMethod.Cash, 2, yesterday), (ClubhousePaymentMethod.Cash, 1, yesterday), (ClubhousePaymentMethod.Card, 3, yesterday), (ClubhousePaymentMethod.Cash, 5, Now) })
            {
                var tab = new ClubhouseTab(ClubId, null, "Tisch", null, paidAt.AddHours(-1));
                tab.Add(beer, quantity, null, paidAt.AddHours(-1));
                tab.MarkPaid(method, null, paidAt);
                ctx.Add(tab);
            }
            await ctx.SaveChangesAsync();
            return 0;
        });
        await Automate(books, AccountingSource.Clubhouse, AccountingAutomationMode.Review, books.Canteen);

        await Sync();

        (await Open()).Select(p => (p.MoneyAccountId, p.AmountCents)).Should().BeEquivalentTo([(books.Cash, 1_200), (books.Card, 1_200)],
            "the cash of today still grows");
    }

    [Fact]
    public async Task Stripe_FeesPerDayAndPayouts_AreBooked()
    {
        var books = await SetUp();
        await QueryAsync(async ctx =>
        {
            ctx.Add(new ClubPaymentAccount(ClubId, "acct_club", Now));
            await ctx.SaveChangesAsync();
            return 0;
        });
        var gateway = GetInstance<IStripeGateway>();
        gateway.IsConfigured.Returns(true);
        var yesterday = Now.AddDays(-1);
        gateway.ListBalanceTransactions("acct_club", Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([
                new StripeBalanceTransaction("txn_1", "charge", 12_000, 199, "eur", yesterday, "ch_1"),
                new StripeBalanceTransaction("txn_2", "charge", 2_500, 61, "eur", yesterday, "ch_2"),
                new StripeBalanceTransaction("txn_3", StripeBalanceTransaction.TypePayout, -14_240, 0, "eur", yesterday, "po_1"),
                new StripeBalanceTransaction("txn_4", "charge", 1_000, 40, "eur", Now, "ch_3"),
            ]);
        await Automate(books, AccountingSource.Stripe, AccountingAutomationMode.Book, books.Fees);

        try
        {
            var result = await Sync();

            result.Booked.Should().Be(2, "the fees of today come tomorrow");
            var booked = await SendAsync(new GetAccountingProposals(ClubId, AccountingProposalStatus.Booked));
            booked.Should().ContainSingle(p => p.Kind == AccountingEntryKind.Booking && p.AmountCents == 260 && p.MoneyAccountId == books.Online);
            booked.Should().ContainSingle(p => p.Kind == AccountingEntryKind.Transfer && p.AmountCents == 14_240 && p.MoneyAccountId == books.Online && p.ToMoneyAccountId == books.Bank);
            (await Sync()).Created.Should().Be(0);
        }
        finally
        {
            gateway.IsConfigured.Returns(false);
        }
    }
}
