using System.Globalization;
using System.Text;
using Bookennis.Api.Business.Accounting;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Accounting;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using InvoiceStatus = Bookennis.Domain.Accounting.InvoiceStatus;
using MoneyAccountKind = Bookennis.Shared.Controller.Accounting.MoneyAccountKind;

namespace Bookennis.Api.Tests.Business.Accounting;

/// <summary>Invoices: settings, drafts, numbers, the PDF, emails and reminders, payment by hand and by a bank line, storno.</summary>
public class InvoiceHandlerTests(TestFixture fixture) : TestBase(fixture)
{
    private const int ClubId = TestDataSeed.ClubId;
    private const int TreasurerUserId = TestDataSeed.AdminId;
    private const string Iban = "AT611904300234573201";
    private static readonly DateTimeOffset Now = new(2026, 5, 4, 10, 0, 0, TimeSpan.Zero);

    private sealed record Books(int Bank, int Sponsoring, int Courts, int Expense, int CostCenter);

    private async Task<Books> SetUp(bool withSettings = true)
    {
        await SendAsync(new SetUpAccounting(ClubId, new SetUpAccountingModel { FiscalYearStartMonth = 1, FirstYear = 2026 }, Now));
        var basics = await SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, false));
        var bank = basics.MoneyAccounts.First(a => a.Kind == MoneyAccountKind.Bank);
        await SendAsync(new UpdateMoneyAccount(ClubId, bank.Id, new SaveMoneyAccountModel { Name = bank.Name, Kind = MoneyAccountKind.Bank, Iban = Iban }));
        var costCenter = await SendAsync(new CreateCostCenter(ClubId, new SaveCostCenterModel { Name = "Jugend" }));
        if (withSettings)
            await SendAsync(new SaveInvoiceSettings(ClubId, new SaveInvoiceSettingsModel { Address = "Sportplatzweg 1\n6800 Feldkirch", ZvrNumber = "123456789", MoneyAccountId = bank.Id, NumberPrefix = "RE", PaymentTermDays = 14, Footer = "Umsatzsteuerbefreit" }));
        int Category(string number) => basics.Categories.Single(c => c.Number == number).Id;
        return new Books(bank.Id, Category("4400"), Category("4100"), Category("5900"), costCenter.Id);
    }

    private static SaveInvoiceModel Model(Books books, DateOnly? date = null, int lines = 1) => new()
    {
        RecipientName = "Autohaus Muster GmbH",
        RecipientAddress = "Hauptstraße 5\n6800 Feldkirch",
        RecipientEmail = "buchhaltung@example.com",
        Subject = "Bandenwerbung 2026",
        Notes = "Vielen Dank für die Unterstützung!",
        ServicePeriod = "Saison 2026",
        InvoiceDate = date ?? new DateOnly(2026, 5, 4),
        DueDate = (date ?? new DateOnly(2026, 5, 4)).AddDays(14),
        Lines = Enumerable.Range(1, lines).Select(i => i == 1
            ? new SaveInvoiceLineModel { Text = "Bande Platz 1", Quantity = 1, UnitPriceCents = 40_000, CategoryId = books.Sponsoring }
            : new SaveInvoiceLineModel { Text = $"Platzmiete Turnier, Tag {i} mit einem langen Text, der in der Rechnung umbrechen muss", Quantity = 2, UnitPriceCents = 2_500, CategoryId = books.Courts, CostCenterId = books.CostCenter }).ToList(),
    };

    private Task<InvoiceDetailDto> Create(Books books, DateOnly? date = null, int lines = 2) => SendAsync(new CreateInvoice(ClubId, Model(books, date, lines), Now));

    private Task<InvoiceDetailDto> Issue(int id) => SendAsync(new IssueInvoice(ClubId, id, Now));

    private static async Task Rejected(Func<Task> act, AccountingErrorCode code)
        => (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(code.ToString());

    [Fact]
    public async Task Settings_NeedABankAccountWithIban_BeforeInvoicesAreIssued()
    {
        var books = await SetUp(withSettings: false);
        var draft = await Create(books);

        (await SendAsync(new GetInvoiceSettings(ClubId))).IsSetUp.Should().BeFalse();
        await Rejected(() => Issue(draft.Id), AccountingErrorCode.AccountingInvoicingNotSetUp);

        var basics = await SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, false));
        var cash = basics.MoneyAccounts.First(a => a.Kind == MoneyAccountKind.Cash).Id;
        await Rejected(() => SendAsync(new SaveInvoiceSettings(ClubId, new SaveInvoiceSettingsModel { Address = "X", MoneyAccountId = cash })), AccountingErrorCode.AccountingInvoiceAccountWithoutIban);

        await SendAsync(new SaveInvoiceSettings(ClubId, new SaveInvoiceSettingsModel { Address = "Sportplatzweg 1", MoneyAccountId = books.Bank, NumberPrefix = "TC-" }));
        (await Issue(draft.Id)).Number.Should().Be("TC-2026-0001");
    }

    [Fact]
    public async Task Numbers_CountPerYear_AndAreNeverGivenTwice()
    {
        var books = await SetUp();
        var first = await Create(books);
        var second = await Create(books);
        var nextYear = await Create(books, new DateOnly(2027, 1, 10));
        var deleted = await Create(books);

        first.Status.Should().Be(Bookennis.Shared.Controller.Accounting.InvoiceStatus.Draft);
        first.TotalCents.Should().Be(45_000);
        await SendAsync(new DeleteInvoice(ClubId, deleted.Id));

        var issued = await Issue(second.Id);
        issued.Number.Should().Be("RE2026-0001");
        issued.InvoiceDate.Should().Be(new DateOnly(2026, 5, 4), "a draft of today keeps its date");
        (await Issue(nextYear.Id)).Number.Should().Be("RE2027-0001");
        await SendAsync(new CancelInvoice(ClubId, second.Id, "Falscher Betrag", Now));
        (await Issue(first.Id)).Number.Should().Be("RE2026-0002", "the cancelled invoice keeps 0001");

        await Rejected(() => SendAsync(new UpdateInvoice(ClubId, first.Id, Model(books), Now)), AccountingErrorCode.AccountingInvoiceNotDraft);
        await Rejected(() => SendAsync(new DeleteInvoice(ClubId, first.Id)), AccountingErrorCode.AccountingInvoiceNotDraft);
        await Rejected(() => Issue(first.Id), AccountingErrorCode.AccountingInvoiceNotDraft);

        var list = await SendAsync(new GetInvoices(ClubId, null, Now));
        list.Invoices.Should().HaveCount(3);
        list.OpenCount.Should().Be(2);
        list.OpenCents.Should().Be(90_000);
        (await SendAsync(new GetInvoices(ClubId, InvoiceStatus.Cancelled, Now))).Invoices.Should().ContainSingle().Which.Number.Should().Be("RE2026-0001");
    }

    [Fact]
    public async Task Lines_UseIncomeCategoriesAndCostCentersOfTheClub()
    {
        var books = await SetUp();
        var model = Model(books);

        await FluentActions.Awaiting(() => SendAsync(new CreateInvoice(ClubId, model with { Lines = [model.Lines[0] with { CategoryId = books.Expense }] }, Now)))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => SendAsync(new CreateInvoice(ClubId, model with { Lines = [model.Lines[0] with { CostCenterId = 987_654 }] }, Now)))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => SendAsync(new CreateInvoice(ClubId, model with { RecipientMemberId = 987_654 }, Now)))
            .Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Paying_BooksAnIncomeEntry_ByCategoryAndCostCenter()
    {
        var books = await SetUp();
        var invoice = await Issue((await Create(books)).Id);

        var paid = await SendAsync(new PayInvoice(ClubId, TreasurerUserId, invoice.Id, new PayInvoiceModel { MoneyAccountId = books.Bank, Date = new DateOnly(2026, 5, 10) }, Now));

        paid.Status.Should().Be(Bookennis.Shared.Controller.Accounting.InvoiceStatus.Paid);
        paid.PaidDate.Should().Be(new DateOnly(2026, 5, 10));
        paid.EntryNumber.Should().Be(1);
        var entry = await SendAsync(new GetAccountingEntry(ClubId, paid.EntryId!.Value));
        entry.Text.Should().Be("Rechnung RE2026-0001 Bandenwerbung 2026");
        entry.CounterpartyName.Should().Be("Autohaus Muster GmbH");
        entry.Receipts.Should().ContainSingle("the invoice is the receipt of the income").Which.FileName.Should().Be("Rechnung-RE2026-0001.pdf");
        (await SendAsync(new GetAccountingReceiptInbox(ClubId))).Should().BeEmpty();
        entry.Lines.Select(l => (l.CategoryId, l.CostCenterId, l.AmountCents)).Should().BeEquivalentTo([(books.Sponsoring, (int?)null, 40_000), (books.Courts, (int?)books.CostCenter, 5_000)]);

        await Rejected(() => SendAsync(new PayInvoice(ClubId, TreasurerUserId, invoice.Id, new PayInvoiceModel { MoneyAccountId = books.Bank, Date = new DateOnly(2026, 5, 10) }, Now)), AccountingErrorCode.AccountingInvoiceNotOpen);
        await Rejected(() => SendAsync(new CancelInvoice(ClubId, invoice.Id, null, Now)), AccountingErrorCode.AccountingInvoiceNotOpen);
    }

    [Fact]
    public async Task BankLine_WithTheInvoiceNumber_IsMatched_AndPaysTheInvoice()
    {
        var books = await SetUp();
        var other = await Issue((await Create(books)).Id);
        var invoice = await Issue((await Create(books)).Id);
        var camt = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08"><BkToCstmrStmt><Stmt>
              <Acct><Id><IBAN>{Iban}</IBAN></Id></Acct>
              <Ntry><Amt Ccy="EUR">450.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Sts><Cd>BOOK</Cd></Sts><BookgDt><Dt>2026-05-12</Dt></BookgDt>
                <NtryDtls><TxDtls><RltdPties><Dbtr><Pty><Nm>Autohaus Muster GmbH</Nm></Pty></Dbtr></RltdPties><RmtInf><Ustrd>Rechnung RE 2026-0002</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry>
            </Stmt></BkToCstmrStmt></Document>
            """;
        await SendAsync(new ImportBankStatement(ClubId, TreasurerUserId, books.Bank, "mai.xml", Encoding.UTF8.GetBytes(camt), null, Now));

        var line = (await SendAsync(new GetBankTransactions(ClubId, books.Bank, Bookennis.Domain.Accounting.BankTransactionStatus.Open))).Transactions.Single();
        line.Matches.Should().HaveCountGreaterThanOrEqualTo(2);
        line.Matches[0].Should().BeEquivalentTo(new { Kind = BankMatchKind.Invoice, Id = invoice.Id }, "the number in the reference comes first");

        await SendAsync(new MatchBankTransaction(ClubId, TreasurerUserId, line.Id, new MatchBankTransactionModel { InvoiceId = invoice.Id }, Now));

        var paid = await SendAsync(new GetInvoice(ClubId, invoice.Id, Now));
        paid.Status.Should().Be(Bookennis.Shared.Controller.Accounting.InvoiceStatus.Paid);
        paid.PaidDate.Should().Be(new DateOnly(2026, 5, 12));
        (await SendAsync(new GetInvoice(ClubId, other.Id, Now))).Status.Should().Be(Bookennis.Shared.Controller.Accounting.InvoiceStatus.Issued);
        (await SendAsync(new GetBankTransactions(ClubId, books.Bank, Bookennis.Domain.Accounting.BankTransactionStatus.Booked))).Transactions.Single().EntryId.Should().Be(paid.EntryId);
    }

    [Fact]
    public async Task Pdf_IsDrawn_ForDraftsAndLongInvoices()
    {
        var books = await SetUp();
        var draft = await Create(books, lines: 30);
        var issued = await Issue((await Create(books)).Id);

        var draftPdf = await SendAsync(new GetInvoicePdf(ClubId, draft.Id));
        var issuedPdf = await SendAsync(new GetInvoicePdf(ClubId, issued.Id));

        Encoding.ASCII.GetString(draftPdf.Data, 0, 5).Should().Be("%PDF-");
        draftPdf.FileName.Should().Be("Rechnung-Entwurf.pdf");
        var pages = System.Text.RegularExpressions.Regex.Match(Encoding.Latin1.GetString(draftPdf.Data), @"/Count (\d+)");
        int.Parse(pages.Groups[1].Value, CultureInfo.InvariantCulture).Should().BeGreaterThan(1, "30 lines don't fit on one page");
        issuedPdf.FileName.Should().Be("Rechnung-RE2026-0001.pdf");
        issuedPdf.Data.Length.Should().BeGreaterThan(5_000);
    }

    [Fact]
    public async Task Sending_EmailsThePdf_AndRemindersAreCounted()
    {
        var books = await SetUp();
        var draft = await Create(books);
        var mediator = Substitute.For<IMediator>();

        Task<InvoiceDetailDto> Send(int id, bool reminder, string? email = null)
            => ScopedAsync(() => new SendInvoice.Handler(GetInstance<AppDbContext>(), mediator).Handle(new SendInvoice(ClubId, id, email, reminder, Now), CancellationToken.None));

        await Rejected(() => Send(draft.Id, false), AccountingErrorCode.AccountingInvoiceNotIssued);
        await Rejected(() => Send(draft.Id, false, "a@x.at, b@y.at"), AccountingErrorCode.AccountingInvoiceEmailInvalid);
        await Issue(draft.Id);

        var sent = await Send(draft.Id, false);
        var reminded = await Send(draft.Id, true, "chef@example.com");

        sent.SentTo.Should().Be("buchhaltung@example.com");
        reminded.ReminderCount.Should().Be(1);
        var emails = mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<SendClubEmail>().ToList();
        emails.Select(e => (e.Type, e.Recipient)).Should().Equal((ClubEmailType.Invoice, "buchhaltung@example.com"), (ClubEmailType.InvoiceReminder, "chef@example.com"));
        var variables = emails[0].Variables.Should().BeOfType<InvoiceEmailVariables>().Subject;
        variables.Invoice.Number.Should().Be("RE2026-0001");
        variables.Invoice.Iban.Should().Be(Iban);
        emails[0].Attachments.Should().ContainSingle().Which.Source.Should().Be(InvoiceAttachmentResolver.CreateUri(draft.Id));

        // The PDF is drawn when the email job sends it, without a tenant
        await using var pdf = await ScopedAsync(() => new InvoiceAttachmentResolver(GetInstance<AppDbContext>()).GetAttachmentAsync(InvoiceAttachmentResolver.CreateUri(draft.Id), CancellationToken.None));
        pdf.Length.Should().BeGreaterThan(1_000);
    }

    [Fact]
    public async Task BankLine_BeforeTheInvoice_MatchesWhenItNamesTheNumber()
    {
        var books = await SetUp();
        var invoice = await Issue((await SendAsync(new CreateInvoice(ClubId, Model(books, new DateOnly(2026, 11, 6), 2), Now))).Id);
        var camt = $"""
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08"><BkToCstmrStmt><Stmt><Acct><Id><IBAN>{Iban}</IBAN></Id></Acct>
              <Ntry><Amt Ccy="EUR">450.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-09-30</Dt></BookgDt>
                <NtryDtls><TxDtls><RmtInf><Ustrd>SEPA-Gutschrift RE2026-0001</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry>
              <Ntry><Amt Ccy="EUR">450.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-09-29</Dt></BookgDt>
                <NtryDtls><TxDtls><RmtInf><Ustrd>Something else</Ustrd></RmtInf></TxDtls></NtryDtls></Ntry>
            </Stmt></BkToCstmrStmt></Document>
            """;
        await SendAsync(new ImportBankStatement(ClubId, TreasurerUserId, books.Bank, "sep.xml", Encoding.UTF8.GetBytes(camt), null, Now));

        var lines = (await SendAsync(new GetBankTransactions(ClubId, books.Bank, Bookennis.Domain.Accounting.BankTransactionStatus.Open))).Transactions;

        lines.Single(l => l.Reference.Contains("RE2026-0001")).Matches.Should().ContainSingle(m => m.Kind == BankMatchKind.Invoice && m.Id == invoice.Id);
        lines.Single(l => l.Reference == "Something else").Matches.Should().NotContain(m => m.Kind == BankMatchKind.Invoice, "an earlier payment without the number is something else");
    }

    [Fact]
    public async Task InvoiceAccount_KeepsItsIban()
    {
        var books = await SetUp();

        await Rejected(() => SendAsync(new UpdateMoneyAccount(ClubId, books.Bank, new SaveMoneyAccountModel { Name = "Bank", Kind = MoneyAccountKind.Bank, Iban = null })),
            AccountingErrorCode.AccountingInvoiceAccountWithoutIban);
    }

    [Fact]
    public async Task ArchivingTheCategoryAfterIssuing_DoesNotBlockThePayment()
    {
        var books = await SetUp();
        var invoice = await Issue((await Create(books)).Id);
        var basics = await SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, false));
        var category = basics.Categories.Single(c => c.Id == books.Sponsoring);
        await SendAsync(new UpdateAccountingCategory(ClubId, category.Id, new SaveAccountingCategoryModel { Kind = category.Kind, Name = category.Name, Number = category.Number, TaxSphere = category.TaxSphere, IsArchived = true }));

        var paid = await SendAsync(new PayInvoice(ClubId, TreasurerUserId, invoice.Id, new PayInvoiceModel { MoneyAccountId = books.Bank, Date = new DateOnly(2026, 5, 10) }, Now));

        paid.Status.Should().Be(Bookennis.Shared.Controller.Accounting.InvoiceStatus.Paid);
        await FluentActions.Awaiting(() => Create(books)).Should().ThrowAsync<PreconditionException>("a new invoice can't use the archived category");
    }

    [Fact]
    public async Task Pdf_WithLongTexts_StaysOnThePage()
    {
        var books = await SetUp();
        await SendAsync(new SaveInvoiceSettings(ClubId, new SaveInvoiceSettingsModel
        {
            Address = "Sportplatzweg 1\n6800 Feldkirch", MoneyAccountId = books.Bank, NumberPrefix = "RE", PaymentTermDays = 14,
            Contact = string.Join(" · ", Enumerable.Repeat("kassier@tc-vorderland.example", 6)),
            Footer = string.Join("\n", Enumerable.Range(1, 5).Select(i => $"Zeile {i} der Fußzeile mit Bankverbindung, Vorstand und dem Hinweis zur Umsatzsteuer, der lang ist")),
        }));
        var model = Model(books) with
        {
            Subject = new string('W', 120),
            ServicePeriod = "Saison 2026 von April bis Oktober mit Turnieren und Kursen",
            Notes = string.Join("\n", Enumerable.Repeat("Ein langer Absatz über das Sponsoring, der umbrochen werden muss, damit er auf die Seite passt.", 9)),
        };
        var invoice = await SendAsync(new CreateInvoice(ClubId, model, Now));

        var pdf = await SendAsync(new GetInvoicePdf(ClubId, invoice.Id));

        Encoding.ASCII.GetString(pdf.Data, 0, 5).Should().Be("%PDF-");
    }
}
