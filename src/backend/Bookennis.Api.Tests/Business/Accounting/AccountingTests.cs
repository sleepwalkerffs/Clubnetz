using Bookennis.Api.Business.Accounting;
using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.Business.ClubAnnouncements;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Accounting;
using ClosedXML.Excel;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AccountingCategoryKind = Bookennis.Shared.Controller.Accounting.AccountingCategoryKind;
using AccountingEntryChangeKind = Bookennis.Shared.Controller.Accounting.AccountingEntryChangeKind;
using AccountingEntryKind = Bookennis.Shared.Controller.Accounting.AccountingEntryKind;
using AccountingEntryStatus = Bookennis.Shared.Controller.Accounting.AccountingEntryStatus;
using FiscalYearAuditStatus = Bookennis.Shared.Controller.Accounting.FiscalYearAuditStatus;
using FiscalYearStatus = Bookennis.Shared.Controller.Accounting.FiscalYearStatus;
using MoneyAccountKind = Bookennis.Shared.Controller.Accounting.MoneyAccountKind;
using TaxSphere = Bookennis.Shared.Controller.Accounting.TaxSphere;

namespace Bookennis.Api.Tests.Business.Accounting;

/// <summary>The accounting handlers: setting up, fiscal years, master data, entries, receipts, reports and the audit.</summary>
public class AccountingTests(TestFixture fixture) : TestBase(fixture)
{
    private const int ClubId = TestDataSeed.ClubId;
    private const int TreasurerUserId = TestDataSeed.AdminId;
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private Task<AccountingDto> Basics() => SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, false));

    private async Task<AccountingDto> SetUp(int startMonth = 1, int firstYear = 2026)
    {
        await SendAsync(new SetUpAccounting(ClubId, new SetUpAccountingModel { FiscalYearStartMonth = startMonth, FirstYear = firstYear }, Now));
        return await Basics();
    }

    private static int Account(AccountingDto basics, MoneyAccountKind kind) => basics.MoneyAccounts.First(a => a.Kind == kind).Id;

    private static int Category(AccountingDto basics, string number) => basics.Categories.Single(c => c.Number == number).Id;

    private Task<AccountingEntryDetailDto> Book(AccountingDto basics, int amountCents, string categoryNumber = "4000", DateOnly? date = null, string text = "Membership fee",
        int? costCenterId = null, List<int>? receiptIds = null, int? counterpartyMemberId = null)
        => SendAsync(new CreateAccountingEntry(ClubId, TreasurerUserId, new SaveAccountingEntryModel
        {
            Date = date ?? new DateOnly(2026, 3, 1),
            Text = text,
            Kind = AccountingEntryKind.Booking,
            MoneyAccountId = Account(basics, MoneyAccountKind.Bank),
            Lines = [new SaveAccountingEntryLineModel { CategoryId = Category(basics, categoryNumber), CostCenterId = costCenterId, AmountCents = amountCents }],
            ReceiptIds = receiptIds ?? [],
            CounterpartyMemberId = counterpartyMemberId,
        }, Now));

    private Task<AccountingEntryDetailDto> Transfer(AccountingDto basics, int amountCents)
        => SendAsync(new CreateAccountingEntry(ClubId, TreasurerUserId, new SaveAccountingEntryModel
        {
            Date = new DateOnly(2026, 4, 1),
            Text = "Cash for the clubhouse",
            Kind = AccountingEntryKind.Transfer,
            MoneyAccountId = Account(basics, MoneyAccountKind.Bank),
            ToMoneyAccountId = Account(basics, MoneyAccountKind.Cash),
            TransferAmountCents = amountCents,
        }, Now));

    private Task<AccountingReceiptDto> Upload(string fileName = "invoice.pdf", int size = 100, byte[]? start = null)
    {
        var data = new byte[size];
        start ??= Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "%PDF-1.7"u8.ToArray(),
            ".jpg" or ".jpeg" => [0xFF, 0xD8, 0xFF, 0xE0],
            ".png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            _ => [],
        };
        start.AsSpan(0, Math.Min(start.Length, size)).CopyTo(data);
        return SendAsync(new UploadAccountingReceipt(ClubId, TreasurerUserId, ClubAnnouncementSeed.File(fileName, data), Now));
    }

    private static async Task Rejected(Func<Task> act, AccountingErrorCode code)
        => (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(code.ToString());

    // ---------- setting up ----------

    [Fact]
    public async Task SetUp_CreatesTheFirstYearTwoAccountsAndTheTemplate_Once()
    {
        (await Basics()).IsSetUp.Should().BeFalse();

        var basics = await SetUp(startMonth: 10, firstYear: 2025);

        basics.IsSetUp.Should().BeTrue();
        basics.FiscalYearStartMonth.Should().Be(10);
        basics.FiscalYears.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Label = "2025/26", StartDate = new DateOnly(2025, 10, 1), Status = FiscalYearStatus.Open }, o => o.ExcludingMissingMembers());
        basics.MoneyAccounts.Select(a => a.Kind).Should().Equal(MoneyAccountKind.Bank, MoneyAccountKind.Cash);
        basics.Categories.Should().Contain(c => c.Number == "4000" && c.Kind == AccountingCategoryKind.Income && c.TaxSphere == TaxSphere.NonProfit);
        basics.Categories.First().Kind.Should().Be(AccountingCategoryKind.Income);
        basics.Categories.Last().Kind.Should().Be(AccountingCategoryKind.Expense);

        await Rejected(() => SetUp(), AccountingErrorCode.AccountingAlreadySetUp);
    }

    [Fact]
    public async Task NothingWorks_BeforeTheAccountingIsSetUp()
    {
        await Rejected(() => SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Bank" })), AccountingErrorCode.AccountingNotSetUp);
        await Rejected(() => SendAsync(new CreateFiscalYear(ClubId, Now)), AccountingErrorCode.AccountingNotSetUp);
        await Rejected(() => Upload(), AccountingErrorCode.AccountingNotSetUp);
    }

    // ---------- entries and reports ----------

    [Fact]
    public async Task Entries_AreNumbered_AndTheReportsAddThemUp()
    {
        var basics = await SetUp();
        var bank = Account(basics, MoneyAccountKind.Bank);
        var cash = Account(basics, MoneyAccountKind.Cash);
        var year = basics.FiscalYears.Single().Id;
        await SendAsync(new SetOpeningBalances(ClubId, year, new SetOpeningBalancesModel { Balances = [new OpeningBalanceModel { MoneyAccountId = bank, AmountCents = 100_000 }] }));
        var youth = await SendAsync(new CreateCostCenter(ClubId, new SaveCostCenterModel { Name = "Jugend" }));

        var fee = await Book(basics, 12_000, counterpartyMemberId: TestDataSeed.Member1Id);
        var balls = await Book(basics, 4_500, "5110", text: "Balls", costCenterId: youth.Id, date: new DateOnly(2026, 5, 2));
        var refund = await Book(basics, -2_000, text: "Fee refunded", date: new DateOnly(2026, 6, 1));
        await Transfer(basics, 30_000);

        fee.Number.Should().Be(1);
        balls.Number.Should().Be(2);
        fee.CounterpartyName.Should().NotBeNullOrEmpty("the member's name is taken when none is written");
        balls.AmountCents.Should().Be(-4_500);

        var summary = await SendAsync(new GetAccountingSummary(ClubId, year));
        summary.Accounts.Single(a => a.MoneyAccountId == bank).Should().BeEquivalentTo(new
        {
            OpeningCents = 100_000L, IncomeCents = 10_000L, ExpenseCents = 4_500L, TransfersInCents = 0L, TransfersOutCents = 30_000L, ClosingCents = 75_500L,
        }, o => o.ExcludingMissingMembers());
        summary.Accounts.Single(a => a.MoneyAccountId == cash).ClosingCents.Should().Be(30_000);
        summary.ClosingCents.Should().Be(105_500);
        summary.EntryCount.Should().Be(4);
        summary.EntriesWithoutReceipt.Should().Be(3, "transfers need no receipt");

        var statement = await SendAsync(new GetIncomeStatement(ClubId, year));
        statement.IncomeCents.Should().Be(10_000);
        statement.ExpenseCents.Should().Be(4_500);
        statement.PreviousFiscalYearId.Should().BeNull();
        statement.Spheres.Should().ContainSingle().Which.Categories.Select(c => c.AmountCents).Should().Equal(10_000, 4_500);

        var costCenters = await SendAsync(new GetCostCenterReport(ClubId, year));
        costCenters.Rows.Select(r => r.CostCenterId).Should().Equal(youth.Id, null);
        costCenters.Rows[0].ExpenseCents.Should().Be(4_500);

        var sheet = await SendAsync(new GetAccountSheet(ClubId, year, bank));
        sheet.Rows.Select(r => r.BalanceCents).Should().Equal(112_000, 82_000, 77_500, 75_500);
        sheet.ClosingCents.Should().Be(75_500);

        var list = await SendAsync(new GetAccountingEntries(ClubId, year, null, null, null, null, false));
        list.Entries.Select(e => e.Number).Should().Equal(3, 2, 4, 1); // newest date first
        list.IncomeCents.Should().Be(10_000);
        (await SendAsync(new GetAccountingEntries(ClubId, year, cash, null, null, null, false))).Entries.Should().ContainSingle().Which.Kind.Should().Be(AccountingEntryKind.Transfer);
        (await SendAsync(new GetAccountingEntries(ClubId, year, null, null, youth.Id, null, false))).Entries.Should().ContainSingle().Which.Id.Should().Be(balls.Id);
        (await SendAsync(new GetAccountingEntries(ClubId, year, null, null, null, "refund", false))).Entries.Should().ContainSingle().Which.Id.Should().Be(refund.Id);
        (await SendAsync(new GetAccountingEntries(ClubId, year, null, null, null, "2", false))).Entries.Should().ContainSingle().Which.Id.Should().Be(balls.Id);

        var reloaded = await Basics();
        reloaded.MoneyAccounts.Should().OnlyContain(a => a.IsUsed);
        reloaded.Categories.Single(c => c.Number == "4000").IsUsed.Should().BeTrue();
        reloaded.CostCenters.Single().IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task Entry_NeedsAFiscalYearForItsDate()
    {
        var basics = await SetUp();

        await Rejected(() => Book(basics, 100, date: new DateOnly(2027, 2, 1)), AccountingErrorCode.AccountingFiscalYearMissing);

        await SendAsync(new CreateFiscalYear(ClubId, Now));
        (await Book(basics, 100, date: new DateOnly(2027, 2, 1))).Number.Should().Be(1, "every fiscal year counts from 1");
    }

    [Fact]
    public async Task Update_KeepsTheHistory_AndMovesReceiptsBetweenInboxAndEntry()
    {
        var basics = await SetUp();
        var first = await Upload("a.pdf");
        var second = await Upload("b.jpg");
        var entry = await Book(basics, 12_000, receiptIds: [first.Id]);

        entry.Receipts.Should().ContainSingle().Which.EntryNumber.Should().Be(1);
        (await SendAsync(new GetAccountingReceiptInbox(ClubId))).Select(r => r.Id).Should().Equal(second.Id);

        var updated = await SendAsync(new UpdateAccountingEntry(ClubId, TreasurerUserId, entry.Id, new SaveAccountingEntryModel
        {
            Date = entry.Date,
            Text = "Membership fee 2026",
            Kind = AccountingEntryKind.Booking,
            MoneyAccountId = entry.MoneyAccountId,
            Lines = [new SaveAccountingEntryLineModel { CategoryId = entry.Lines[0].CategoryId, AmountCents = 13_000 }],
            ReceiptIds = [second.Id],
        }, Now.AddHours(1)));

        updated.Text.Should().Be("Membership fee 2026");
        updated.Receipts.Select(r => r.Id).Should().Equal(second.Id);
        updated.History.Select(h => h.Kind).Should().Equal(AccountingEntryChangeKind.Created, AccountingEntryChangeKind.Changed);
        updated.History[0].Lines.Single().AmountCents.Should().Be(12_000);
        updated.History[0].ReceiptIds.Should().Equal(first.Id);
        updated.History[1].ByName.Should().NotBeNullOrEmpty();
        (await SendAsync(new GetAccountingReceiptInbox(ClubId))).Select(r => r.Id).Should().Equal(first.Id);

        // A receipt of an entry belongs to it: it can't go onto another entry or be deleted
        await Rejected(() => Book(basics, 100, receiptIds: [second.Id]), AccountingErrorCode.AccountingReceiptAlreadyBooked);
        await Rejected(() => SendAsync(new DeleteAccountingReceipt(ClubId, second.Id)), AccountingErrorCode.AccountingReceiptAlreadyBooked);
        await SendAsync(new DeleteAccountingReceipt(ClubId, first.Id));
        (await SendAsync(new GetAccountingReceiptInbox(ClubId))).Should().BeEmpty();
    }

    [Fact]
    public async Task CancelledEntry_StaysListed_ButNoLongerCounts()
    {
        var basics = await SetUp();
        var year = basics.FiscalYears.Single().Id;
        var entry = await Book(basics, 12_000);

        var cancelled = await SendAsync(new CancelAccountingEntry(ClubId, TreasurerUserId, entry.Id, "Booked twice", Now));

        cancelled.Status.Should().Be(AccountingEntryStatus.Cancelled);
        cancelled.CancelReason.Should().Be("Booked twice");
        (await SendAsync(new GetAccountingSummary(ClubId, year))).Should().BeEquivalentTo(new { IncomeCents = 0L, EntryCount = 0, CancelledCount = 1 }, o => o.ExcludingMissingMembers());
        (await SendAsync(new GetAccountingEntries(ClubId, year, null, null, null, null, false))).Entries.Should().BeEmpty();
        (await SendAsync(new GetAccountingEntries(ClubId, year, null, null, null, null, true))).Entries.Should().ContainSingle();
        (await Book(basics, 100)).Number.Should().Be(2, "the number of a cancelled entry is not given again");
    }

    [Fact]
    public async Task ArchivedCategory_IsNotOfferedForNewEntries_ButStaysOnOldOnes()
    {
        var basics = await SetUp();
        var entry = await Book(basics, 12_000);
        var category = basics.Categories.Single(c => c.Number == "4000");
        await SendAsync(new UpdateAccountingCategory(ClubId, category.Id, new SaveAccountingCategoryModel { Kind = category.Kind, Number = category.Number, Name = category.Name, TaxSphere = category.TaxSphere, IsArchived = true }));

        await Rejected(() => Book(basics, 100), AccountingErrorCode.AccountingArchived);

        var updated = await SendAsync(new UpdateAccountingEntry(ClubId, TreasurerUserId, entry.Id, new SaveAccountingEntryModel
        {
            Date = entry.Date, Text = "Changed", Kind = AccountingEntryKind.Booking, MoneyAccountId = entry.MoneyAccountId,
            Lines = [new SaveAccountingEntryLineModel { CategoryId = category.Id, AmountCents = 12_000 }],
        }, Now));
        updated.Text.Should().Be("Changed");
    }

    [Fact]
    public async Task Entry_RefersOnlyToTheClubsOwnData()
    {
        var basics = await SetUp();

        await FluentActions.Awaiting(() => Book(basics, 100, counterpartyMemberId: 987_654)).Should().ThrowAsync<EntityNotFoundException>();
        var entry = await Book(basics, 100);
        await FluentActions.Awaiting(() => SendAsync(new GetAccountingEntry(ClubId + 1, entry.Id))).Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => SendAsync(new GetAccountingSummary(ClubId + 1, basics.FiscalYears[0].Id))).Should().ThrowAsync<EntityNotFoundException>();
    }

    // ---------- fiscal years ----------

    [Fact]
    public async Task ClosingAYear_LocksIt_AndCarriesTheBalancesIntoTheNextYear()
    {
        var basics = await SetUp(firstYear: 2025);
        var bank = Account(basics, MoneyAccountKind.Bank);
        var year = basics.FiscalYears.Single();
        await SendAsync(new SetOpeningBalances(ClubId, year.Id, new SetOpeningBalancesModel { Balances = [new OpeningBalanceModel { MoneyAccountId = bank, AmountCents = 50_000 }] }));
        var entry = await Book(basics, 12_000, date: new DateOnly(2025, 6, 1));

        var closed = await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, year.Id, Now));

        closed.Status.Should().Be(FiscalYearStatus.Closed);
        closed.ClosedByName.Should().NotBeNullOrEmpty();
        var years = (await Basics()).FiscalYears;
        years.Select(y => y.Label).Should().Equal("2026", "2025");
        years[0].OpeningBalancesCarried.Should().BeTrue();
        years[0].OpeningBalances.Single(b => b.MoneyAccountId == bank).AmountCents.Should().Be(62_000);

        await Rejected(() => Book(basics, 100, date: new DateOnly(2025, 7, 1)), AccountingErrorCode.AccountingFiscalYearClosed);
        await Rejected(() => SendAsync(new CancelAccountingEntry(ClubId, TreasurerUserId, entry.Id, null, Now)), AccountingErrorCode.AccountingFiscalYearClosed);
        await Rejected(() => SendAsync(new SetOpeningBalances(ClubId, years[0].Id, new SetOpeningBalancesModel { Balances = [new OpeningBalanceModel { MoneyAccountId = bank, AmountCents = 1 }] })),
            AccountingErrorCode.AccountingOpeningBalancesCarried);

        // The year before counts in the income statement of the next one
        await Book(basics, 15_000, date: new DateOnly(2026, 6, 1));
        var statement = await SendAsync(new GetIncomeStatement(ClubId, years[0].Id));
        statement.PreviousIncomeCents.Should().Be(12_000);
        statement.IncomeCents.Should().Be(15_000);

        // Reopened and corrected: the next year follows at once, not only when the year is closed again
        await SendAsync(new ReopenFiscalYear(ClubId, year.Id));
        var correction = await Book(basics, 1_000, date: new DateOnly(2025, 12, 31));
        (await Basics()).FiscalYears[0].OpeningBalances.Single(b => b.MoneyAccountId == bank).AmountCents.Should().Be(63_000);
        await SendAsync(new CancelAccountingEntry(ClubId, TreasurerUserId, correction.Id, "Wrong year", Now));
        (await Basics()).FiscalYears[0].OpeningBalances.Single(b => b.MoneyAccountId == bank).AmountCents.Should().Be(62_000);

        await Book(basics, 1_000, date: new DateOnly(2025, 12, 30));
        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, year.Id, Now));
        (await Basics()).FiscalYears[0].OpeningBalances.Single(b => b.MoneyAccountId == bank).AmountCents.Should().Be(63_000);
    }

    [Fact]
    public async Task NextYear_FollowsTheYearBefore_AndAccountsItDidntUseGetTheirBalanceByHand()
    {
        var basics = await SetUp(firstYear: 2025);
        var bank = Account(basics, MoneyAccountKind.Bank);
        var first = basics.FiscalYears.Single();
        await SendAsync(new SetOpeningBalances(ClubId, first.Id, new SetOpeningBalancesModel { Balances = [new OpeningBalanceModel { MoneyAccountId = bank, AmountCents = 50_000 }] }));

        // Created while the year before is still open: it starts with its balances right away
        var next = await SendAsync(new CreateFiscalYear(ClubId, Now));
        next.OpeningBalancesCarried.Should().BeTrue();
        next.OpeningBalances.Should().ContainSingle(b => b.MoneyAccountId == bank && b.AmountCents == 50_000 && b.IsCarried);
        await Book(basics, 2_000, date: new DateOnly(2025, 5, 1));
        (await Basics()).FiscalYears[0].OpeningBalances.Single(b => b.MoneyAccountId == bank).AmountCents.Should().Be(52_000);

        // A savings book the club only adds in the second year: what was on it is entered by hand and stays when the year before closes
        var savings = await SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Sparbuch", Kind = MoneyAccountKind.Bank }));
        await SendAsync(new SetOpeningBalances(ClubId, next.Id, new SetOpeningBalancesModel { Balances = [new OpeningBalanceModel { MoneyAccountId = savings.Id, AmountCents = 500_000 }] }));
        await Rejected(() => SendAsync(new SetOpeningBalances(ClubId, next.Id, new SetOpeningBalancesModel { Balances = [new OpeningBalanceModel { MoneyAccountId = bank, AmountCents = 1 }] })),
            AccountingErrorCode.AccountingOpeningBalancesCarried);

        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, first.Id, Now));
        var balances = (await Basics()).FiscalYears[0].OpeningBalances;
        balances.Should().ContainSingle(b => b.MoneyAccountId == savings.Id && b.AmountCents == 500_000 && !b.IsCarried);
        balances.Should().ContainSingle(b => b.MoneyAccountId == bank && b.AmountCents == 52_000 && b.IsCarried);
    }

    [Fact]
    public async Task ClosingAYear_AlwaysCreatesTheNextOne_AlsoAYearAhead()
    {
        var basics = await SetUp(firstYear: 2027);

        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, basics.FiscalYears.Single().Id, Now));

        (await Basics()).FiscalYears.Select(y => y.Label).Should().Equal("2028", "2027");
    }

    [Fact]
    public async Task Years_AreClosedInOrder()
    {
        var basics = await SetUp(firstYear: 2025);
        var next = await SendAsync(new CreateFiscalYear(ClubId, Now));

        await Rejected(() => SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, next.Id, Now)), AccountingErrorCode.AccountingPreviousYearOpen);
        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, basics.FiscalYears[0].Id, Now));
        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, next.Id, Now));
        await Rejected(() => SendAsync(new ReopenFiscalYear(ClubId, basics.FiscalYears[0].Id)), AccountingErrorCode.AccountingNextYearClosed);
        await Rejected(() => SendAsync(new CreateFiscalYear(ClubId, Now)), AccountingErrorCode.AccountingFiscalYearTooFarAhead);
    }

    [Fact]
    public async Task Audit_IsRecordedByAnAuditorWhoIsNoTreasurer()
    {
        var basics = await SetUp(firstYear: 2025);
        var year = basics.FiscalYears.Single().Id;
        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, year, Now));
        var model = new RecordAuditModel { Status = FiscalYearAuditStatus.Audited, Comment = "Checked all receipts" };

        // The admin of the test club passes the policy, but is no auditor
        await Rejected(() => SendAsync(new RecordFiscalYearAudit(ClubId, TreasurerUserId, year, model, Now)), AccountingErrorCode.AccountingAuditorRequired);

        var (auditorUserId, auditorMemberId) = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "auditor@test.com", MemberRole.Auditor));
        var audited = await SendAsync(new RecordFiscalYearAudit(ClubId, auditorUserId, year, model, Now));

        audited.AuditStatus.Should().Be(FiscalYearAuditStatus.Audited);
        audited.AuditComment.Should().Be("Checked all receipts");
        audited.AuditedByName.Should().Contain("auditor");

        await QueryAsync(async ctx =>
        {
            var member = await ctx.ClubMembers.SingleAsync(m => m.Id == auditorMemberId);
            member.UpdateRoles([MemberRole.Auditor, MemberRole.Treasurer]);
            await ctx.SaveChangesAsync();
            return 0;
        });
        await Rejected(() => SendAsync(new RecordFiscalYearAudit(ClubId, auditorUserId, year, model, Now)), AccountingErrorCode.AccountingAuditorIsTreasurer);

        // An admin keeps the books as well
        await QueryAsync(async ctx =>
        {
            var member = await ctx.ClubMembers.SingleAsync(m => m.Id == auditorMemberId);
            member.UpdateRoles([MemberRole.Auditor, MemberRole.Admin]);
            await ctx.SaveChangesAsync();
            return 0;
        });
        await Rejected(() => SendAsync(new RecordFiscalYearAudit(ClubId, auditorUserId, year, model, Now)), AccountingErrorCode.AccountingAuditorIsTreasurer);
    }

    [Fact]
    public async Task CanAudit_OnlyForWhoMayRecordTheAudit()
    {
        await SetUp();
        var (auditorUserId, _) = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "auditor@test.com", MemberRole.Auditor));

        // Admins pass every policy of the club, the button would only lead to an error
        (await SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, true))).CanAudit.Should().BeFalse();
        (await SendAsync(new GetAccounting(ClubId, auditorUserId, false, true))).CanAudit.Should().BeTrue();
        (await SendAsync(new GetAccounting(ClubId, auditorUserId, false, false))).CanAudit.Should().BeFalse();
    }

    // ---------- master data ----------

    [Fact]
    public async Task MasterData_HasUniqueNames_AndIsDeletedOnlyWhileUnused()
    {
        var basics = await SetUp();

        var savings = await SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Sparbuch", Kind = MoneyAccountKind.Bank, Iban = "AT61 1904 3002 3457 3201", SortOrder = 5 }));
        savings.Iban.Should().Be("AT611904300234573201");
        await Rejected(() => SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "sparbuch", Kind = MoneyAccountKind.Cash })), AccountingErrorCode.AccountingNameTaken);
        await Rejected(() => SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Bad", Kind = MoneyAccountKind.Bank, Iban = "AT00 1234" })), AccountingErrorCode.AccountingIbanInvalid);
        await SendAsync(new DeleteMoneyAccount(ClubId, savings.Id));

        // The same name is fine for an income and an expense category
        var canteenIncome = basics.Categories.Single(c => c.Number == "4300");
        var category = await SendAsync(new CreateAccountingCategory(ClubId, new SaveAccountingCategoryModel { Kind = AccountingCategoryKind.Expense, Name = canteenIncome.Name, TaxSphere = TaxSphere.AuxiliaryOperation }));
        category.Kind.Should().Be(AccountingCategoryKind.Expense);
        await Rejected(() => SendAsync(new CreateAccountingCategory(ClubId, new SaveAccountingCategoryModel { Kind = AccountingCategoryKind.Income, Name = canteenIncome.Name.ToUpperInvariant() })),
            AccountingErrorCode.AccountingNameTaken);

        await Book(basics, 100, "4300");
        await Rejected(() => SendAsync(new DeleteAccountingCategory(ClubId, canteenIncome.Id)), AccountingErrorCode.AccountingInUse);
        await Rejected(() => SendAsync(new DeleteMoneyAccount(ClubId, Account(basics, MoneyAccountKind.Bank))), AccountingErrorCode.AccountingInUse);
        await Rejected(() => SendAsync(new UpdateAccountingCategory(ClubId, canteenIncome.Id, new SaveAccountingCategoryModel { Kind = AccountingCategoryKind.Expense, Name = "Other name" })),
            AccountingErrorCode.AccountingCategoryKindInUse);
        // Used in an open year only: the tax sphere may still change
        await SendAsync(new UpdateAccountingCategory(ClubId, canteenIncome.Id, new SaveAccountingCategoryModel { Kind = AccountingCategoryKind.Income, Name = canteenIncome.Name, Number = canteenIncome.Number, TaxSphere = TaxSphere.CommercialOperation }));
        await SendAsync(new DeleteAccountingCategory(ClubId, category.Id));

        var costCenter = await SendAsync(new CreateCostCenter(ClubId, new SaveCostCenterModel { Name = "Clubmeisterschaft" }));
        await SendAsync(new UpdateCostCenter(ClubId, costCenter.Id, new SaveCostCenterModel { Name = "Club championship", IsArchived = true }));
        await SendAsync(new DeleteCostCenter(ClubId, costCenter.Id));
        (await Basics()).CostCenters.Should().BeEmpty();
    }

    [Fact]
    public async Task CategoryUsedInAClosedYear_KeepsItsTaxSphere()
    {
        var basics = await SetUp(firstYear: 2025);
        var category = basics.Categories.Single(c => c.Number == "4000");
        await Book(basics, 100, date: new DateOnly(2025, 3, 1));
        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, basics.FiscalYears.Single().Id, Now));

        SaveAccountingCategoryModel Model(TaxSphere sphere, string name) => new() { Kind = category.Kind, Name = name, Number = category.Number, TaxSphere = sphere };
        var other = category.TaxSphere == TaxSphere.NonProfit ? TaxSphere.CommercialOperation : TaxSphere.NonProfit;
        await Rejected(() => SendAsync(new UpdateAccountingCategory(ClubId, category.Id, Model(other, category.Name))), AccountingErrorCode.AccountingCategorySphereInUse);

        // Renaming changes nothing the year adds up
        var renamed = await SendAsync(new UpdateAccountingCategory(ClubId, category.Id, Model(category.TaxSphere, "Mitgliedsbeiträge neu")));
        renamed.Name.Should().Be("Mitgliedsbeiträge neu");
    }

    // ---------- receipts ----------

    [Fact]
    public async Task Receipts_AreUploadedIntoTheInbox_AndOnlyAllowedTypes()
    {
        await SetUp();

        var receipt = await Upload("Rechnung Platzpflege.PDF", 1234);

        receipt.ContentType.Should().Be("application/pdf");
        receipt.Size.Should().Be(1234);
        receipt.EntryId.Should().BeNull();
        var file = await SendAsync(new GetAccountingReceiptFile(ClubId, receipt.Id));
        file.Data.Should().HaveCount(1234);
        file.FileName.Should().Be("Rechnung Platzpflege.PDF");

        await Rejected(() => Upload("script.html"), AccountingErrorCode.AccountingReceiptTypeNotAllowed);
        await Rejected(() => Upload("empty.pdf", 0), AccountingErrorCode.AccountingReceiptTypeNotAllowed);
        await Rejected(() => Upload("renamed.pdf", start: "<html><script>"u8.ToArray()), AccountingErrorCode.AccountingReceiptTypeNotAllowed);
        await Rejected(() => Upload("photo.jpg", start: [0x89, 0x50, 0x4E, 0x47]), AccountingErrorCode.AccountingReceiptTypeNotAllowed);
        await Rejected(() => Upload("huge.pdf", AccountingReceipt.MaxSizeInBytes + 1), AccountingErrorCode.AccountingReceiptTooLarge);
    }

    // ---------- other ----------

    [Fact]
    public async Task Members_AreFoundByName_NamesOnly()
    {
        await SetUp();

        var all = await SendAsync(new GetAccountingMembers(ClubId, null));
        all.Should().HaveCount(2);

        var first = all[0].Name.Split(' ')[0];
        (await SendAsync(new GetAccountingMembers(ClubId, first[..2]))).Should().Contain(m => m.Name == all[0].Name);
        (await SendAsync(new GetAccountingMembers(ClubId, "zzzz"))).Should().BeEmpty();
    }

    [Fact]
    public async Task Export_HasTheJournalTheStatementAndTheAccounts()
    {
        var basics = await SetUp();
        await Book(basics, 12_000);
        await Transfer(basics, 5_000);

        var result = await SendAsync(new ExportAccounting(ClubId, basics.FiscalYears[0].Id));

        result.FileName.Should().EndWith("2026.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(result.Data));
        workbook.Worksheets.Should().HaveCount(3);
        var journal = workbook.Worksheet(1);
        journal.Cell(2, 9).GetValue<decimal>().Should().Be(120m);
        journal.Cell(3, 11).GetValue<decimal>().Should().Be(50m);
    }

    [Fact]
    public async Task AnnualReport_IsAPdfOfTheYear()
    {
        var basics = await SetUp();
        await Book(basics, 12_000);
        await Transfer(basics, 5_000);

        var result = await SendAsync(new GetAnnualReport(ClubId, basics.FiscalYears[0].Id, Now));

        System.Text.Encoding.ASCII.GetString(result.Data, 0, 5).Should().Be("%PDF-");
        result.FileName.Should().EndWith(" 2026.pdf");
        System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.Latin1.GetString(result.Data), @"/Count (\d+)").Groups[1].Value.Should().Be("1");
    }

    [Fact]
    public async Task AnnualReport_WithoutEntries_IsStillDrawn()
    {
        var basics = await SetUp();

        var result = await SendAsync(new GetAnnualReport(ClubId, basics.FiscalYears[0].Id, Now));

        System.Text.Encoding.ASCII.GetString(result.Data, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Export_ShowsCancelledEntriesWithoutAmounts()
    {
        var basics = await SetUp();
        var entry = await Book(basics, 12_000);
        await SendAsync(new CancelAccountingEntry(ClubId, TreasurerUserId, entry.Id, "Booked twice", Now));

        var result = await SendAsync(new ExportAccounting(ClubId, basics.FiscalYears[0].Id));

        using var workbook = new XLWorkbook(new MemoryStream(result.Data));
        var journal = workbook.Worksheet(1);
        journal.Cell(2, 1).GetValue<int>().Should().Be(entry.Number);
        journal.Cell(2, 9).IsEmpty().Should().BeTrue();
        journal.Cell(2, 13).GetString().Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeletingTheClub_RemovesItsAccounting()
    {
        var basics = await SetUp();
        var receipt = await Upload();
        await Book(basics, 12_000, receiptIds: [receipt.Id]);
        await SendAsync(new CloseFiscalYear(ClubId, TreasurerUserId, basics.FiscalYears[0].Id, Now));

        await SendAsync(new DeleteAdminClub(ClubId));

        (await QueryAsync(ctx => ctx.AccountingEntries.IgnoreQueryFilters().CountAsync())).Should().Be(0);
        (await QueryAsync(ctx => ctx.FiscalYears.IgnoreQueryFilters().CountAsync())).Should().Be(0);
        (await QueryAsync(ctx => ctx.AccountingReceipts.IgnoreQueryFilters().CountAsync())).Should().Be(0);
    }
}
