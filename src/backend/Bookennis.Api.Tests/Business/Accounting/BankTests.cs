using System.Globalization;
using System.Text;
using Bookennis.Api.Business.Accounting;
using Bookennis.Api.Business.Admin;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Accounting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AccountingEntryKind = Bookennis.Shared.Controller.Accounting.AccountingEntryKind;
using BankRuleDirection = Bookennis.Shared.Controller.Accounting.BankRuleDirection;
using BankTransactionStatus = Bookennis.Domain.Accounting.BankTransactionStatus;
using MoneyAccountKind = Bookennis.Shared.Controller.Accounting.MoneyAccountKind;

namespace Bookennis.Api.Tests.Business.Accounting;

/// <summary>Bank statements: import, matching with entries and suggestions, booking with rules, the balance check.</summary>
public class BankTests(TestFixture fixture) : TestBase(fixture)
{
    private const int ClubId = TestDataSeed.ClubId;
    private const int TreasurerUserId = TestDataSeed.AdminId;
    private const string Iban = "AT611904300234573201";
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private sealed record Books(int Bank, int Fees, int Membership, int Association);

    private async Task<Books> SetUp()
    {
        await SendAsync(new SetUpAccounting(ClubId, new SetUpAccountingModel { FiscalYearStartMonth = 1, FirstYear = 2026 }, Now));
        var basics = await SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, false));
        var bank = basics.MoneyAccounts.First(a => a.Kind == MoneyAccountKind.Bank);
        await SendAsync(new UpdateMoneyAccount(ClubId, bank.Id, new SaveMoneyAccountModel { Name = bank.Name, Kind = MoneyAccountKind.Bank, Iban = Iban }));
        await SendAsync(new SetOpeningBalances(ClubId, basics.FiscalYears[0].Id, new SetOpeningBalancesModel { Balances = [new OpeningBalanceModel { MoneyAccountId = bank.Id, AmountCents = 100_000 }] }));
        int Category(string number) => basics.Categories.Single(c => c.Number == number).Id;
        return new Books(bank.Id, Category("5710"), Category("4000"), Category("5000"));
    }

    private static string Camt(string iban, params (string Date, decimal Amount, string Name, string Text)[] lines)
    {
        var entries = string.Concat(lines.Select(l => $"""
            <Ntry><Amt Ccy="EUR">{Math.Abs(l.Amount).ToString("0.00", CultureInfo.InvariantCulture)}</Amt><CdtDbtInd>{(l.Amount > 0 ? "CRDT" : "DBIT")}</CdtDbtInd><Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>{l.Date}</Dt></BookgDt>
              <NtryDtls><TxDtls><RltdPties><{(l.Amount > 0 ? "Dbtr" : "Cdtr")}><Nm>{l.Name}</Nm></{(l.Amount > 0 ? "Dbtr" : "Cdtr")}></RltdPties><RmtInf><Ustrd>{l.Text}</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry>
            """));
        var closing = 1000m + lines.Sum(l => l.Amount);
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"><BkToCstmrStmt><Stmt>
              <Acct><Id><IBAN>{iban}</IBAN></Id></Acct>
              <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">{closing.ToString("0.00", CultureInfo.InvariantCulture)}</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-05-31</Dt></Dt></Bal>
              {entries}
            </Stmt></BkToCstmrStmt></Document>
            """;
    }

    private Task<BankImportResultDto> Import(int accountId, string content, string fileName = "auszug.xml", CsvColumnsDto? layout = null)
        => SendAsync(new ImportBankStatement(ClubId, TreasurerUserId, accountId, fileName, Encoding.UTF8.GetBytes(content), layout, Now));

    private Task<GetBankTransactionsResult> Lines(BankTransactionStatus status = BankTransactionStatus.Open)
        => SendAsync(new GetBankTransactions(ClubId, null, status));

    private Task<AccountingEntryDetailDto> Book(int accountId, int categoryId, int amountCents, string date)
        => SendAsync(new CreateAccountingEntry(ClubId, TreasurerUserId, new SaveAccountingEntryModel
        {
            Date = DateOnly.Parse(date, CultureInfo.InvariantCulture), Text = "Gebucht", Kind = AccountingEntryKind.Booking, MoneyAccountId = accountId,
            Lines = [new SaveAccountingEntryLineModel { CategoryId = categoryId, AmountCents = amountCents }],
        }, Now));

    private static async Task Rejected(Func<Task> act, AccountingErrorCode code)
        => (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(code.ToString());

    [Fact]
    public async Task Import_AddsEachLineOnce_AndChecksTheBalance()
    {
        var books = await SetUp();
        var file = Camt(Iban, ("2026-05-02", 120m, "Anna Huber", "Beitrag"), ("2026-05-10", -30m, "ÖTV", "Lizenz"));

        var first = await Import(books.Bank, file);
        var again = await Import(books.Bank, file);

        first.Imported.Should().Be(2);
        again.Imported.Should().Be(0);
        again.Skipped.Should().Be(2);
        var lines = await Lines();
        lines.OpenCount.Should().Be(2);
        lines.Transactions.Select(t => (t.AmountCents, t.CounterpartyName)).Should().Equal((-3_000, "ÖTV"), (12_000, "Anna Huber"));

        // The bank says 1,090.00 on 31 May; the books say 1,000.00 until both lines are booked
        var import = (await SendAsync(new GetBankStatementImports(ClubId, books.Bank))).First();
        import.ClosingBalanceCents.Should().Be(109_000);
        import.BooksBalanceCents.Should().Be(100_000);

        await Rejected(() => Import(books.Bank, Camt("AT483200000012345864", ("2026-05-02", 1m, "X", "Y"))), AccountingErrorCode.AccountingBankFileOtherAccount);
    }

    [Fact]
    public async Task Csv_WithUnknownColumns_IsMappedOnce_ForTheAccount()
    {
        var books = await SetUp();
        const string csv = "Tag|Wert|Partner|Zweck\n02.05.2026|120,00|Anna Huber|Beitrag\n03.05.2026|-30,00|ÖTV|Lizenz\n";

        var unknown = await Import(books.Bank, csv, "export.csv");
        unknown.NeedsCsvLayout.Should().BeTrue();
        unknown.Preview[1].Should().Equal("02.05.2026", "120,00", "Anna Huber", "Beitrag");
        (await Lines()).OpenCount.Should().Be(0);

        var layout = new CsvColumnsDto { Delimiter = "|", SkipRows = 1, BookingDate = 0, Amount = 1, CounterpartyName = 2, Reference = [3] };
        await Rejected(() => Import(books.Bank, csv, "export.csv", layout with { Amount = 2 }), AccountingErrorCode.AccountingBankCsvLayoutInvalid);
        (await Import(books.Bank, csv, "export.csv", layout)).Imported.Should().Be(2);

        // The next export of the account is read with the saved columns
        var next = await Import(books.Bank, "Tag|Wert|Partner|Zweck\n04.05.2026|-5,00|Bank|Spesen\n", "export2.csv");
        next.NeedsCsvLayout.Should().BeFalse();
        next.Imported.Should().Be(1);
    }

    [Fact]
    public async Task Line_IsMatchedWithTheEntryOfItsAmount_AndReopenedWhenTheEntryIsCancelled()
    {
        var books = await SetUp();
        await Import(books.Bank, Camt(Iban, ("2026-05-02", 120m, "Anna Huber", "Beitrag")));
        var entry = await Book(books.Bank, books.Membership, 12_000, "2026-05-01");
        await Book(books.Bank, books.Membership, 12_000, "2026-06-30"); // too far away

        var line = (await Lines()).Transactions.Single();
        line.Matches.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Kind = BankMatchKind.Entry, Id = entry.Id, Number = entry.Number });

        await SendAsync(new MatchBankTransaction(ClubId, TreasurerUserId, line.Id, new MatchBankTransactionModel { EntryId = entry.Id }, Now));
        var booked = (await Lines(BankTransactionStatus.Booked)).Transactions.Single();
        booked.EntryNumber.Should().Be(entry.Number);
        (await Lines()).OpenCount.Should().Be(0);

        await SendAsync(new CancelAccountingEntry(ClubId, TreasurerUserId, entry.Id, "Doppelt", Now));
        (await Lines()).Transactions.Should().ContainSingle(t => t.Id == line.Id, "the line is open again");
    }

    [Fact]
    public async Task Line_IsBookedAsNewEntry_OnlyWithItsAmount()
    {
        var books = await SetUp();
        await Import(books.Bank, Camt(Iban, ("2026-05-10", -30m, "ÖTV", "Lizenz")));
        var line = (await Lines()).Transactions.Single();
        SaveAccountingEntryModel Model(int amount) => new()
        {
            Date = line.BookingDate, Text = "ÖTV Lizenz", Kind = AccountingEntryKind.Booking, MoneyAccountId = books.Bank,
            Lines = [new SaveAccountingEntryLineModel { CategoryId = books.Association, AmountCents = amount }],
        };

        await Rejected(() => SendAsync(new BookBankTransaction(ClubId, TreasurerUserId, line.Id, Model(2_000), Now)), AccountingErrorCode.AccountingBankTransactionAmountMismatch);
        (await QueryAsync(ctx => ctx.AccountingEntries.CountAsync(e => e.ClubId == ClubId))).Should().Be(0, "nothing is saved when the amount does not fit");

        var entry = await SendAsync(new BookBankTransaction(ClubId, TreasurerUserId, line.Id, Model(3_000), Now));
        entry.Number.Should().Be(1);
        (await Lines(BankTransactionStatus.Booked)).Transactions.Single().EntryId.Should().Be(entry.Id);
    }

    [Fact]
    public async Task Line_IsMatchedWithASuggestion_WhichIsBookedThen()
    {
        var books = await SetUp();
        await QueryAsync(async ctx =>
        {
            ctx.Add(new AccountingProposal(ClubId, Domain.Accounting.AccountingSource.MembershipFees, "membership-order:1",
                new AccountingProposal.ProposalData(new DateOnly(2026, 5, 1), "Mitgliedsbeitrag Erwachsene", Domain.Accounting.AccountingEntryKind.Booking, books.Bank, null, books.Membership, 12_000, null, "Anna Huber"), Now));
            await ctx.SaveChangesAsync();
            return 0;
        });
        await Import(books.Bank, Camt(Iban, ("2026-05-02", 120m, "Anna Huber", "Beitrag")));

        var line = (await Lines()).Transactions.Single();
        var match = line.Matches.Should().ContainSingle().Subject;
        match.Kind.Should().Be(BankMatchKind.Proposal);

        await SendAsync(new MatchBankTransaction(ClubId, TreasurerUserId, line.Id, new MatchBankTransactionModel { ProposalId = match.Id }, Now));

        (await SendAsync(new GetAccountingProposals(ClubId, Domain.Accounting.AccountingProposalStatus.Booked))).Should().ContainSingle();
        (await Lines()).OpenCount.Should().Be(0);
    }

    [Fact]
    public async Task Rules_BookTheLinesTheyFit_UnlessAnEntryFitsAlready()
    {
        var books = await SetUp();
        await Import(books.Bank, Camt(Iban, ("2026-05-10", -30m, "ÖTV Wien", "Lizenz"), ("2026-05-11", -45m, "ÖTV Wien", "Meisterschaft"), ("2026-05-12", -5m, "Bank", "Spesen")));
        await Book(books.Bank, books.Association, 4_500, "2026-05-11");
        var rule = await SendAsync(new CreateBankRule(ClubId, new SaveBankRuleModel { MatchText = "ötv", Direction = BankRuleDirection.Outgoing, CategoryId = books.Association, Text = "Verbandsbeitrag ÖTV" }));
        await Rejected(() => SendAsync(new CreateBankRule(ClubId, new SaveBankRuleModel { CategoryId = books.Fees })), AccountingErrorCode.AccountingBankRuleInvalid);

        var lines = (await Lines()).Transactions;
        lines.Where(t => t.RuleId == rule.Id).Should().HaveCount(2);

        var result = await SendAsync(new BookBankTransactionsByRules(ClubId, TreasurerUserId, books.Bank, Now));

        result.Booked.Should().Be(1, "the 45.00 line has an entry of its own to match");
        var entries = await SendAsync(new GetAccountingEntries(ClubId, (await QueryAsync(ctx => ctx.FiscalYears.SingleAsync(y => y.ClubId == ClubId))).Id, null, null, null, null, false));
        entries.Entries.Should().Contain(e => e.Text == "Verbandsbeitrag ÖTV" && e.AmountCents == -3_000);
        (await Lines()).Transactions.Select(t => t.AmountCents).Should().BeEquivalentTo([-4_500, -500]);
    }

    [Fact]
    public async Task Transfer_BetweenTwoImportedAccounts_IsMatchedOnBothSides()
    {
        var books = await SetUp();
        const string savingsIban = "AT483200000012345864";
        var savings = await SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Sparbuch", Kind = MoneyAccountKind.Bank, Iban = savingsIban }));
        var transfer = await SendAsync(new CreateAccountingEntry(ClubId, TreasurerUserId, new SaveAccountingEntryModel
        {
            Date = new DateOnly(2026, 5, 5), Text = "Auf das Sparbuch", Kind = AccountingEntryKind.Transfer, MoneyAccountId = books.Bank, ToMoneyAccountId = savings.Id, TransferAmountCents = 50_000,
        }, Now));
        await Import(books.Bank, Camt(Iban, ("2026-05-05", -500m, "TC Vorderland", "Sparbuch")));
        await Import(savings.Id, Camt(savingsIban, ("2026-05-05", 500m, "TC Vorderland", "Sparbuch")));

        foreach (var account in new[] { books.Bank, savings.Id })
        {
            var line = (await SendAsync(new GetBankTransactions(ClubId, account, BankTransactionStatus.Open))).Transactions.Single();
            line.Matches.Should().ContainSingle().Which.Id.Should().Be(transfer.Id);
            await SendAsync(new MatchBankTransaction(ClubId, TreasurerUserId, line.Id, new MatchBankTransactionModel { EntryId = transfer.Id }, Now));
        }

        (await Lines()).OpenCount.Should().Be(0);
    }

    [Fact]
    public async Task IgnoredLine_ComesBack_WhenReopened()
    {
        var books = await SetUp();
        await Import(books.Bank, Camt(Iban, ("2026-05-12", -5m, "Bank", "Spesen")));
        var line = (await Lines()).Transactions.Single();

        await SendAsync(new IgnoreBankTransaction(ClubId, line.Id));
        (await Lines()).OpenCount.Should().Be(0);
        await Rejected(() => SendAsync(new IgnoreBankTransaction(ClubId, line.Id)), AccountingErrorCode.AccountingBankTransactionNotOpen);

        await SendAsync(new ReopenBankTransaction(ClubId, line.Id));
        (await Lines()).OpenCount.Should().Be(1);
    }

    [Fact]
    public async Task UsedMoneyAccount_IsNotDeleted_AndDeletingTheClubRemovesTheBank()
    {
        var books = await SetUp();
        var savings = await SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Sparbuch", Kind = MoneyAccountKind.Bank }));
        await Import(savings.Id, Camt("AT483200000012345864", ("2026-05-12", 5m, "Bank", "Zinsen")));
        await SendAsync(new CreateBankRule(ClubId, new SaveBankRuleModel { MatchText = "Zinsen", CategoryId = books.Membership, MoneyAccountId = savings.Id }));

        await Rejected(() => SendAsync(new DeleteMoneyAccount(ClubId, savings.Id)), AccountingErrorCode.AccountingInUse);

        await SendAsync(new DeleteAdminClub(ClubId));
        (await QueryAsync(ctx => ctx.BankTransactions.IgnoreQueryFilters().CountAsync(t => t.ClubId == ClubId))).Should().Be(0);
        (await QueryAsync(ctx => ctx.BankRules.IgnoreQueryFilters().CountAsync(r => r.ClubId == ClubId))).Should().Be(0);
    }
}
