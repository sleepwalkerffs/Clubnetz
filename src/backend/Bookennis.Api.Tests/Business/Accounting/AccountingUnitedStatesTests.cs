using System.Text;
using System.Text.RegularExpressions;
using Bookennis.Api.Business.Accounting;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.Business.CourtSales;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Accounting;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using AccountingAutomationMode = Bookennis.Shared.Controller.Accounting.AccountingAutomationMode;
using AccountingProposalStatus = Bookennis.Domain.Accounting.AccountingProposalStatus;
using AccountingSource = Bookennis.Shared.Controller.Accounting.AccountingSource;
using BankTransactionStatus = Bookennis.Domain.Accounting.BankTransactionStatus;
using MoneyAccountKind = Bookennis.Shared.Controller.Accounting.MoneyAccountKind;
using TaxSphere = Bookennis.Shared.Controller.Accounting.TaxSphere;

namespace Bookennis.Api.Tests.Business.Accounting;

/// <summary>
/// The accounting of a club in Ashburn, Virginia: New York time, dollars, a US bank account (account and routing number), the US chart
/// of accounts, US Letter PDFs without the EPC QR code and the CSV export of a US bank.
/// </summary>
public class AccountingUnitedStatesTests(TestFixture fixture) : TestBase(fixture)
{
    private const int ClubId = TestDataSeed.ClubId;
    private const int TreasurerUserId = TestDataSeed.AdminId;
    private const string AccountNumber = "000123456789";
    private const string RoutingNumber = "021000021";

    /// <summary>New Year's Eve, 21:00 in New York: already 2027 in UTC (and in Vienna).</summary>
    private static readonly DateTimeOffset NewYearsEve = new(2027, 1, 1, 2, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private sealed record Books(AccountingDto Basics, int Bank, int Dues, int Courts);

    private Task MoveClubToVirginia()
        => QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.SingleAsync(c => c.Id == ClubId);
            club.SetRegion(Country.UnitedStates, "America/New_York", "usd");
            await ctx.SaveChangesAsync();
            return 0;
        });

    private async Task<Books> SetUp(DateTimeOffset now, bool withBankDetails = true)
    {
        await MoveClubToVirginia();
        await SendAsync(new SetUpAccounting(ClubId, new SetUpAccountingModel { FiscalYearStartMonth = 1, FirstYear = 2026 }, now));
        var basics = await SendAsync(new GetAccounting(ClubId, TreasurerUserId, true, false));
        var bank = basics.MoneyAccounts.First(a => a.Kind == MoneyAccountKind.Bank);
        if (withBankDetails)
            await SendAsync(new UpdateMoneyAccount(ClubId, bank.Id, new SaveMoneyAccountModel { Name = bank.Name, Kind = MoneyAccountKind.Bank, AccountNumber = "0001-2345-6789", RoutingNumber = "021 000 021" }));
        int Category(string number) => basics.Categories.Single(c => c.Number == number).Id;
        return new Books(basics, bank.Id, Category("4000"), Category("4100"));
    }

    private async Task SaveSettings(Books books)
        => await SendAsync(new SaveInvoiceSettings(ClubId, new SaveInvoiceSettingsModel
        {
            Address = "123 Main Street\nAshburn, VA 20147", ZvrNumber = "12-3456789", MoneyAccountId = books.Bank, NumberPrefix = "INV", PaymentTermDays = 30,
        }));

    private static SaveInvoiceModel Invoice(Books books, DateOnly date) => new()
    {
        RecipientName = "Loudoun Sports LLC",
        RecipientEmail = "billing@example.com",
        Subject = "Court banner 2026",
        InvoiceDate = date,
        DueDate = date.AddDays(30),
        Lines = [new SaveInvoiceLineModel { Text = "Banner court 1", Quantity = 1, UnitPriceCents = 50_000, CategoryId = books.Courts }],
    };

    private static float[] MediaBox(byte[] pdf)
    {
        var match = Regex.Match(Encoding.Latin1.GetString(pdf), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        match.Success.Should().BeTrue();
        return [float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)];
    }

    private static async Task Rejected(Func<Task> act, AccountingErrorCode code)
        => (await act.Should().ThrowAsync<Bookennis.Domain.Exceptions.PreconditionException>()).Which.ErrorCode.Should().Be(code.ToString());

    [Fact]
    public async Task SetUp_GivesAUsClubTheUsChart_WithoutTaxSpheres_AndItsRegion()
    {
        var books = await SetUp(NewYearsEve, withBankDetails: false);

        var basics = books.Basics;
        basics.Country.Should().Be(Bookennis.Shared.Controller.Shared.Country.UnitedStates);
        basics.Currency.Should().Be("usd");
        basics.TimeZoneId.Should().Be("America/New_York");
        basics.Categories.Select(c => c.Name).Should().Contain(["Mitgliedsbeiträge", "USTA- und Ligagebühren"]);
        basics.Categories.Should().OnlyContain(c => c.TaxSphere == TaxSphere.NonProfit, "a US club has no tax spheres");
        basics.FiscalYears.Should().ContainSingle().Which.Label.Should().Be("2026", "New Year's Eve in New York is still 2026");
    }

    [Fact]
    public async Task UsBankAccount_IsChecked_AndNeededForInvoices()
    {
        var books = await SetUp(NewYearsEve, withBankDetails: false);
        var bank = books.Basics.MoneyAccounts.Single(a => a.Id == books.Bank);
        SaveMoneyAccountModel Account(string? number, string? routing) => new() { Name = bank.Name, Kind = MoneyAccountKind.Bank, AccountNumber = number, RoutingNumber = routing };

        await Rejected(() => SendAsync(new SaveInvoiceSettings(ClubId, new SaveInvoiceSettingsModel { Address = "X", MoneyAccountId = books.Bank })),
            AccountingErrorCode.AccountingInvoiceAccountWithoutBankDetails);
        await Rejected(() => SendAsync(new UpdateMoneyAccount(ClubId, books.Bank, Account(AccountNumber, "021000022"))), AccountingErrorCode.AccountingRoutingNumberInvalid);
        await Rejected(() => SendAsync(new UpdateMoneyAccount(ClubId, books.Bank, Account(AccountNumber, null))), AccountingErrorCode.AccountingRoutingNumberInvalid);
        await Rejected(() => SendAsync(new UpdateMoneyAccount(ClubId, books.Bank, Account("12", RoutingNumber))), AccountingErrorCode.AccountingAccountNumberInvalid);

        var saved = await SendAsync(new UpdateMoneyAccount(ClubId, books.Bank, Account("0001-2345-6789", "021 000 021")));
        saved.AccountNumber.Should().Be(AccountNumber);
        saved.RoutingNumber.Should().Be(RoutingNumber);
        saved.Iban.Should().BeNull();

        await SaveSettings(books);
        await Rejected(() => SendAsync(new UpdateMoneyAccount(ClubId, books.Bank, Account(null, null))), AccountingErrorCode.AccountingInvoiceAccountWithoutBankDetails);
    }

    [Fact]
    public async Task Invoice_IsDatedTheLocalDay_InDollars_OnLetter_WithoutQrCode()
    {
        var books = await SetUp(NewYearsEve);
        await SaveSettings(books);
        var draft = await SendAsync(new CreateInvoice(ClubId, Invoice(books, new DateOnly(2026, 12, 20)), NewYearsEve));

        var issued = await SendAsync(new IssueInvoice(ClubId, draft.Id, NewYearsEve));

        issued.InvoiceDate.Should().Be(new DateOnly(2026, 12, 31), "it is 9 PM on New Year's Eve in New York");
        issued.Number.Should().Be("INV2026-0001");
        issued.IsOverdue.Should().BeFalse();

        var pdf = await SendAsync(new GetInvoicePdf(ClubId, issued.Id));
        MediaBox(pdf.Data).Should().Equal(612f, 792f);

        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(() => new SendInvoice.Handler(GetInstance<AppDbContext>(), mediator).Handle(new SendInvoice(ClubId, issued.Id, null, false, NewYearsEve), CancellationToken.None));
        var email = mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<SendClubEmail>().Single();
        var variables = email.Variables.Should().BeOfType<InvoiceEmailVariables>().Subject.Invoice;
        // The invoice is in German (the language of who wrote it): German numbers, US dates
        variables.Amount.Should().Be("$ 500,00");
        variables.Date.Should().Be("12/31/2026");
        variables.DueDate.Should().Be("1/30/2027");
        variables.Iban.Should().BeNull();
        variables.AccountNumber.Should().Be(AccountNumber);
        variables.RoutingNumber.Should().Be(RoutingNumber);
        variables.QrCode.Should().BeFalse("the EPC QR code is a SEPA transfer in euros");

        foreach (var (type, language) in new[] { (ClubEmailType.Invoice, Language.English), (ClubEmailType.InvoiceReminder, Language.English), (ClubEmailType.Invoice, Language.German), (ClubEmailType.InvoiceReminder, Language.German) })
        {
            var template = ClubEmailDefaultTemplates.Get(type, language);
            new ClubEmailRenderer().Validate(ClubEmailCatalog.Get(type), template);
            var rendered = await new ClubEmailRenderer().Render(template, email.Variables);
            rendered.BodyHtml.Should().Contain(RoutingNumber).And.Contain(AccountNumber).And.NotContain("IBAN").And.NotContain("QR");
        }
    }

    [Fact]
    public async Task InvoiceEmails_OfAnAustrianClub_KeepTheIbanAndTheQrCode()
    {
        var variables = new InvoiceEmailVariables(new MemberVariables("Max", "Muster"),
            new InvoiceVariables("RE2026-0001", "€ 450,00", "04.05.2026", "18.05.2026", null, "AT611904300234573201", 0));

        foreach (var type in new[] { ClubEmailType.Invoice, ClubEmailType.InvoiceReminder })
        {
            var rendered = await new ClubEmailRenderer().Render(ClubEmailDefaultTemplates.Get(type, Language.German), variables);
            rendered.BodyHtml.Should().Contain("AT611904300234573201").And.NotContain("Routing");
            if (type == ClubEmailType.Invoice)
                rendered.BodyHtml.Should().Contain("QR-Code");
        }
    }

    [Fact]
    public async Task AnnualReport_AndExport_AreDrawnForAUsClub()
    {
        var books = await SetUp(NewYearsEve);
        var year = books.Basics.FiscalYears.Single();
        await SendAsync(new CreateAccountingEntry(ClubId, TreasurerUserId, new SaveAccountingEntryModel
        {
            Date = new DateOnly(2026, 6, 1), Text = "Dues", Kind = Bookennis.Shared.Controller.Accounting.AccountingEntryKind.Booking, MoneyAccountId = books.Bank,
            Lines = [new SaveAccountingEntryLineModel { CategoryId = books.Dues, AmountCents = 120_000 }],
        }, NewYearsEve));

        var report = await SendAsync(new GetAnnualReport(ClubId, year.Id, NewYearsEve));
        var export = await SendAsync(new ExportAccounting(ClubId, year.Id));

        MediaBox(report.Data).Should().Equal(612f, 792f);
        export.Data.Length.Should().BeGreaterThan(1_000);
    }

    [Fact]
    public async Task CsvOfAUsBank_IsImported_MonthFirst()
    {
        var books = await SetUp(NewYearsEve);
        const string chase = """
            Details,Posting Date,Description,Amount,Type,Balance,Check or Slip #,
            CREDIT,03/04/2026,"LOUDOUN SPORTS LLC BANNER","1,234.56",ACH_CREDIT,"5,234.56",,
            DEBIT,03/05/2026,"USTA MID-ATLANTIC DUES",-45.00,ACH_DEBIT,"5,189.56",,
            CHECK,03/12/2026,"CHECK 1043",($300.00),CHECK_PAID,"4,889.56",1043,
            """;

        var result = await SendAsync(new ImportBankStatement(ClubId, TreasurerUserId, books.Bank, "chase.csv", Encoding.UTF8.GetBytes(chase), null, NewYearsEve));

        result.NeedsCsvLayout.Should().BeFalse();
        result.Imported.Should().Be(3);
        var lines = (await SendAsync(new GetBankTransactions(ClubId, books.Bank, BankTransactionStatus.Open))).Transactions;
        lines.Select(l => (l.BookingDate, l.AmountCents)).Should().BeEquivalentTo(
            [(new DateOnly(2026, 3, 4), 123_456), (new DateOnly(2026, 3, 5), -4_500), (new DateOnly(2026, 3, 12), -30_000)]);
    }

    [Fact]
    public async Task AutomaticEntries_UseTheLocalTimeAndFormatsOfTheClub()
    {
        var now = DateTimeOffset.UtcNow;
        var books = await SetUp(now);
        var online = await SendAsync(new CreateMoneyAccount(ClubId, new SaveMoneyAccountModel { Name = "Stripe", Kind = MoneyAccountKind.PaymentProvider }));
        var (_, buyerId) = await QueryAsync(ctx => CourtSaleSeed.AddBuyer(ctx));
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, buyerId, CourtSaleSeed.Tomorrow(), priceCents: 2_500));
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, NewYork).DateTime);
        await SendAsync(new UpdateAccountingAutomation(ClubId, new SaveAccountingAutomationModel
        {
            OnlineMoneyAccountId = online.Id,
            BankMoneyAccountId = books.Bank,
            Sources = [new AccountingSourceSettingDto { Source = AccountingSource.CourtSales, Mode = AccountingAutomationMode.Review, CategoryId = books.Courts, Since = today.AddDays(-30) }],
        }, now));

        await SendAsync(new SyncAccountingProposals(ClubId, now));

        var proposal = (await SendAsync(new GetAccountingProposals(ClubId, AccountingProposalStatus.Open))).Should().ContainSingle().Subject;
        proposal.Date.Should().Be(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(purchase.PaidAt!.Value, NewYork).DateTime));
        var start = TimeZoneInfo.ConvertTime(purchase.Interval.From, NewYork);
        proposal.Text.Should().EndWith($"{start.Month}/{start.Day}/{start.Year} {(start.Hour % 12 == 0 ? 12 : start.Hour % 12)}:{start.Minute:00} {(start.Hour < 12 ? "AM" : "PM")}",
            "a US club writes the month first and the 12-hour clock");
    }
}
