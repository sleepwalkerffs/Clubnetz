using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Accounting;

public class InvoiceTests
{
    private const int ClubId = 7;
    private static readonly DateTimeOffset Now = new(2026, 5, 4, 10, 0, 0, TimeSpan.Zero);

    private static InvoiceSettings Settings(string prefix = "RE")
        => new(ClubId, new InvoiceSettings.SettingsData("Sportplatzweg 1\n6800 Feldkirch", "123456789", null, 3, "bkauatww", prefix, 14, "Umsatzsteuerbefreit"));

    private static Invoice.InvoiceData Data(params Invoice.LineData[] lines) => new(
        null, "Autohaus Muster GmbH", "Hauptstraße 5\n6800 Feldkirch", "buchhaltung@example.com", "Bandenwerbung 2026", null, "Saison 2026",
        new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 18),
        lines.Length > 0 ? lines : [new Invoice.LineData("Bande Platz 1", 1, 45_000, 1, null)]);

    private static void Rejected(Action act, AccountingErrorCode code)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(code.ToString());

    [Fact]
    public void Total_IsTheSumOfTheLines_WithQuantitiesAndDiscounts()
    {
        var invoice = new Invoice(ClubId, Data(
            new Invoice.LineData("Trainerstunden", 2.5m, 4_000, 1, null),
            new Invoice.LineData("Platzmiete", 3, 1_833, 1, 2),
            new Invoice.LineData("Rabatt", 1, -1_000, 1, null)), german: true);

        invoice.Lines.Select(l => (l.Position, l.TotalCents)).Should().Equal((1, 10_000), (2, 5_499), (3, -1_000));
        invoice.TotalCents.Should().Be(14_499);
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.Number.Should().BeNull();
    }

    [Fact]
    public void Invoice_NeedsAName_ValidDates_AndAPositiveTotal()
    {
        Rejected(() => new Invoice(ClubId, Data() with { RecipientName = " " }, true), AccountingErrorCode.AccountingInvoiceTextInvalid);
        Rejected(() => new Invoice(ClubId, Data() with { DueDate = new DateOnly(2026, 5, 1) }, true), AccountingErrorCode.AccountingInvoiceDueDateInvalid);
        Rejected(() => new Invoice(ClubId, Data(new Invoice.LineData("Gutschrift", 1, -500, 1, null)), true), AccountingErrorCode.AccountingAmountInvalid);
        Rejected(() => new Invoice(ClubId, Data(new Invoice.LineData("Halb", 0.333m, 100, 1, null)), true), AccountingErrorCode.AccountingInvoiceQuantityInvalid);
        Rejected(() => new Invoice(ClubId, Data() with { RecipientEmail = "kein mail" }, true), AccountingErrorCode.AccountingInvoiceEmailInvalid);
        Rejected(() => new Invoice(ClubId, Data() with { Lines = [] }, true), AccountingErrorCode.AccountingLinesInvalid);
    }

    [Fact]
    public void IssuedInvoice_HasItsNumber_AndIsFinal()
    {
        var invoice = new Invoice(ClubId, Data(), german: true);

        invoice.Issue(Settings(), 7, Now);

        invoice.Number.Should().Be("RE2026-0007");
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        Rejected(() => invoice.Update(Data()), AccountingErrorCode.AccountingInvoiceNotDraft);
        invoice.IsOverdue(new DateOnly(2026, 5, 18)).Should().BeFalse();
        invoice.IsOverdue(new DateOnly(2026, 5, 19)).Should().BeTrue();

        invoice.RecordReminder(Now);
        invoice.ReminderCount.Should().Be(1);
        invoice.Cancel("Falsche Adresse", Now);
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.Number.Should().Be("RE2026-0007", "a cancelled invoice keeps its number");
        Rejected(() => invoice.Cancel(null, Now), AccountingErrorCode.AccountingInvoiceNotOpen);
        Rejected(() => invoice.RecordReminder(Now), AccountingErrorCode.AccountingInvoiceNotOpen);
    }

    [Fact]
    public void Draft_OfAPastDay_MovesToTheIssueDay_WithItsTerm()
    {
        var past = new Invoice(ClubId, Data() with { InvoiceDate = new DateOnly(2026, 11, 20), DueDate = new DateOnly(2026, 12, 4) }, german: true);
        var future = new Invoice(ClubId, Data() with { InvoiceDate = new DateOnly(2027, 2, 1), DueDate = new DateOnly(2027, 2, 15) }, german: true);

        past.MoveDatesForIssue(new DateOnly(2027, 1, 10));
        future.MoveDatesForIssue(new DateOnly(2027, 1, 10));

        (past.InvoiceDate, past.DueDate).Should().Be((new DateOnly(2027, 1, 10), new DateOnly(2027, 1, 24)));
        (future.InvoiceDate, future.DueDate).Should().Be((new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 15)));
    }

    [Fact]
    public void Draft_IsNotSentOrReminded()
    {
        var invoice = new Invoice(ClubId, Data(), german: false);

        Rejected(() => invoice.RecordSent("a@b.at", Now), AccountingErrorCode.AccountingInvoiceNotIssued);
        Rejected(() => invoice.RecordReminder(Now), AccountingErrorCode.AccountingInvoiceNotIssued);
    }

    [Fact]
    public void Settings_CheckBicPrefixAndTerm()
    {
        Settings().Bic.Should().Be("BKAUATWW");
        Settings("").NumberOf(2026, 12).Should().Be("2026-0012");
        Rejected(() => new InvoiceSettings(ClubId, new InvoiceSettings.SettingsData("A", null, null, 1, "ABC", "RE", 14, null)), AccountingErrorCode.AccountingInvoiceSettingsInvalid);
        Rejected(() => new InvoiceSettings(ClubId, new InvoiceSettings.SettingsData("A", null, null, 1, null, "RE 1", 14, null)), AccountingErrorCode.AccountingInvoiceSettingsInvalid);
        Rejected(() => new InvoiceSettings(ClubId, new InvoiceSettings.SettingsData("A", null, null, 1, null, "RE", 400, null)), AccountingErrorCode.AccountingInvoiceSettingsInvalid);
        Rejected(() => new InvoiceSettings(ClubId, new InvoiceSettings.SettingsData(" ", null, null, 1, null, "RE", 14, null)), AccountingErrorCode.AccountingInvoiceTextInvalid);
    }

    [Theory]
    [InlineData("buchhaltung@example.com", true)]
    [InlineData("a@x.at, b@y.at", false)]
    [InlineData("a@x.at;b@y.at", false)]
    [InlineData("Max <max@x.at>", false)]
    [InlineData("max@x.at\nBcc: c@d.at", false)]
    [InlineData("kein mail", false)]
    public void Email_IsOnePlainAddress(string value, bool valid) => Invoice.IsEmail(value).Should().Be(valid);

    [Fact]
    public void CancelledInvoice_IsNotSent()
    {
        var invoice = new Invoice(ClubId, Data(), german: true);
        invoice.Issue(Settings(), 1, Now);
        invoice.Cancel(null, Now);

        Rejected(() => invoice.RecordSent("a@b.at", Now), AccountingErrorCode.AccountingInvoiceNotOpen);
    }

    [Fact]
    public void EpcQrPayload_FollowsTheStandard()
    {
        var payload = EpcQrPayload.For("TC Vorderland\nVerein", "AT61 1904 3002 3457 3201", null, 45_000, "RE2026-0007");

        payload.Split('\n').Should().Equal("BCD", "002", "1", "SCT", "", "TC Vorderland Verein", "AT611904300234573201", "EUR450.00", "", "", "RE2026-0007");
    }
}
