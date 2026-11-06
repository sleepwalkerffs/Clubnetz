using System.Globalization;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>
/// What every invoice of the club says about the club: address, ZVR number (Vereinsregister), the account to pay to, the numbering and
/// payment terms, and a footer (e.g. the tax note). No row = invoicing not set up yet.
/// </summary>
public class InvoiceSettings : TenantDomainEntity, IAggregateRoot
{
    public const int MaxAddressLength = 300;
    public const int MaxZvrLength = 20;
    public const int MaxContactLength = 200;
    public const int MaxPrefixLength = 10;
    public const int MaxFooterLength = 600;
    public const int MaxBicLength = 11;

#pragma warning disable CS8618
    private InvoiceSettings() { }
#pragma warning restore CS8618

    public InvoiceSettings(int clubId, SettingsData data)
    {
        ClubId = clubId;
        Change(data);
    }

    /// <summary>
    /// <c>MoneyAccountId</c>: the bank account whose IBAN the invoices name (checked by the caller: of the club, with an IBAN).
    /// <c>NumberPrefix</c>: "RE" gives RE2026-0001.
    /// </summary>
    public record SettingsData(string? Address, string? ZvrNumber, string? Contact, int MoneyAccountId, string? Bic, string? NumberPrefix, int PaymentTermDays, string? Footer);

    public string Address { get; private set; } = "";
    public string? ZvrNumber { get; private set; }

    /// <summary>Email, phone, website: one line under the address.</summary>
    public string? Contact { get; private set; }

    public int MoneyAccountId { get; private set; }
    public string? Bic { get; private set; }
    public string NumberPrefix { get; private set; } = "";
    public int PaymentTermDays { get; private set; }

    /// <summary>The text at the end of every invoice, e.g. "Umsatzsteuerbefreit gemäß § 6 Abs. 1 Z 27 UStG".</summary>
    public string? Footer { get; private set; }

    public void Change(SettingsData data)
    {
        Address = Text(data.Address, MaxAddressLength, required: true)!;
        ZvrNumber = Text(data.ZvrNumber, MaxZvrLength);
        Contact = Text(data.Contact, MaxContactLength);
        Footer = Text(data.Footer, MaxFooterLength);
        var bic = Text(data.Bic, MaxBicLength)?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (bic is not null && bic.Length is not (8 or 11))
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceSettingsInvalid, "Invalid BIC.");
        Bic = bic;

        var prefix = Text(data.NumberPrefix, MaxPrefixLength) ?? "";
        if (prefix.Any(c => !char.IsLetterOrDigit(c) && c is not ('-' or '/' or '_')))
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceSettingsInvalid, "Invalid prefix.");
        NumberPrefix = prefix;

        if (data.PaymentTermDays is < 0 or > 365)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceSettingsInvalid, "Invalid payment term.");
        PaymentTermDays = data.PaymentTermDays;
        MoneyAccountId = data.MoneyAccountId;
    }

    /// <summary>RE2026-0001: prefix, year of the invoice date, number in that year.</summary>
    public string NumberOf(int year, int sequence)
        => $"{NumberPrefix}{year.ToString(CultureInfo.InvariantCulture)}-{sequence.ToString("0000", CultureInfo.InvariantCulture)}";

    internal static string? Text(string? value, int maxLength, bool required = false)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (required && text is null || text?.Length > maxLength)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceTextInvalid, [maxLength.ToString(CultureInfo.InvariantCulture)], "Invalid text.");
        return text;
    }
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.InvoiceStatus</c>.</summary>
public enum InvoiceStatus
{
    /// <summary>Being written: no number yet, can be changed or deleted.</summary>
    Draft = 0,

    /// <summary>Has its number and is final: waits for the money.</summary>
    Issued = 1,

    /// <summary>The money came in: booked as an income entry.</summary>
    Paid = 2,

    /// <summary>Cancelled (Storno): keeps its number, never paid.</summary>
    Cancelled = 3,
}

/// <summary>
/// An invoice of the club (to a sponsor, a member for a course, another club for a tournament). Written as a draft, issued with the next
/// number of its year (never reused, also when cancelled), sent as a PDF with a payment QR code, reminded when overdue and paid with an
/// income entry (by hand or by a matching bank line).
/// </summary>
public class Invoice : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNameLength = 120;
    public const int MaxAddressLength = 300;
    public const int MaxEmailLength = 254;
    public const int MaxSubjectLength = 120;
    public const int MaxNotesLength = 1000;
    public const int MaxServicePeriodLength = 60;
    public const int MaxLines = 30;
    public const int MaxNumberLength = 30;
    public const int MaxCancelReasonLength = 200;

    private readonly List<InvoiceLine> lines = new();

#pragma warning disable CS8618
    private Invoice() { }
#pragma warning restore CS8618

    /// <summary>A new draft. With <paramref name="german"/> the invoice, its PDF and its emails are in German (else English): the language of who wrote it.</summary>
    public Invoice(int clubId, InvoiceData data, bool german)
    {
        ClubId = clubId;
        Status = InvoiceStatus.Draft;
        German = german;
        Apply(data);
    }

    public bool German { get; private set; }

    /// <summary>A line: <c>Quantity</c> with up to two decimals, <c>UnitPriceCents</c> in cents, booked to its category (an income) and cost center when paid.</summary>
    public record LineData(string? Text, decimal Quantity, int UnitPriceCents, int CategoryId, int? CostCenterId);

    /// <summary>The invoice as written. <c>RecipientMemberId</c> links a member of the club (the name and address stay as written).</summary>
    public record InvoiceData(
        int? RecipientMemberId,
        string? RecipientName,
        string? RecipientAddress,
        string? RecipientEmail,
        string? Subject,
        string? Notes,
        string? ServicePeriod,
        DateOnly InvoiceDate,
        DateOnly DueDate,
        IReadOnlyList<LineData> Lines);

    public InvoiceStatus Status { get; private set; }

    /// <summary>RE2026-0001, set when the invoice is issued.</summary>
    public string? Number { get; private set; }

    public int? NumberYear { get; private set; }
    public int? NumberSequence { get; private set; }

    public int? RecipientMemberId { get; private set; }
    public string RecipientName { get; private set; } = "";
    public string? RecipientAddress { get; private set; }
    public string? RecipientEmail { get; private set; }
    public string? Subject { get; private set; }

    /// <summary>Text above the lines (thanks, what the invoice is about).</summary>
    public string? Notes { get; private set; }

    /// <summary>When the service was given (Leistungszeitraum), e.g. "Saison 2026" or "12.–14. Juni 2026".</summary>
    public string? ServicePeriod { get; private set; }

    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public int TotalCents { get; private set; }

    public DateTimeOffset? IssuedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? SentTo { get; private set; }

    /// <summary>How many reminders were sent (Mahnstufe).</summary>
    public int ReminderCount { get; private set; }

    public DateTimeOffset? LastReminderAt { get; private set; }

    /// <summary>The income entry that books the payment.</summary>
    public int? EntryId { get; private set; }

    public DateOnly? PaidDate { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => lines;

    public bool IsOpen => Status == InvoiceStatus.Issued;

    /// <summary>Issued, unpaid and past its due date.</summary>
    public bool IsOverdue(DateOnly today) => IsOpen && today > DueDate;

    /// <summary>Changes a draft. An issued invoice is final: cancel it and write a new one.</summary>
    public void Update(InvoiceData data)
    {
        EnsureDraft();
        Apply(data);
    }

    public void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceNotDraft, [Number ?? ""], "Only a draft can be changed.");
    }

    /// <summary>Gives the draft its number (the next of the year of its date, found by the caller under the accounting lock).</summary>
    public void Issue(InvoiceSettings settings, int sequence, DateTimeOffset now)
    {
        EnsureDraft();
        Status = InvoiceStatus.Issued;
        NumberYear = InvoiceDate.Year;
        NumberSequence = sequence;
        Number = settings.NumberOf(InvoiceDate.Year, sequence);
        IssuedAt = now;
    }

    public void RecordSent(string email, DateTimeOffset now)
    {
        EnsureIssued();
        SentAt = now;
        SentTo = email;
    }

    public void RecordReminder(DateTimeOffset now)
    {
        EnsureIssued();
        if (Status != InvoiceStatus.Issued)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceNotOpen, [Number ?? ""], "The invoice is paid or cancelled.");
        ReminderCount++;
        LastReminderAt = now;
    }

    /// <summary>The money came in: <paramref name="entry"/> books it.</summary>
    public void MarkPaid(AccountingEntry entry, DateOnly date)
    {
        EnsureOpen();
        Status = InvoiceStatus.Paid;
        EntryId = entry.Id;
        PaidDate = date;
    }

    /// <summary>Storno: the invoice keeps its number and is never paid. A paid invoice is not cancelled (refund the money with an entry).</summary>
    public void Cancel(string? reason, DateTimeOffset now)
    {
        EnsureOpen();
        var text = InvoiceSettings.Text(reason, MaxCancelReasonLength);
        Status = InvoiceStatus.Cancelled;
        CancelledAt = now;
        CancelReason = text;
    }

    public void EnsureOpen()
    {
        if (Status != InvoiceStatus.Issued)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceNotOpen, [Number ?? ""], "The invoice is not waiting for its money.");
    }

    private void EnsureIssued()
    {
        if (Status == InvoiceStatus.Draft)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceNotIssued, "Issue the invoice first.");
    }

    private void Apply(InvoiceData data)
    {
        RecipientMemberId = data.RecipientMemberId;
        RecipientName = InvoiceSettings.Text(data.RecipientName, MaxNameLength, required: true)!;
        RecipientAddress = InvoiceSettings.Text(data.RecipientAddress, MaxAddressLength);
        var email = InvoiceSettings.Text(data.RecipientEmail, MaxEmailLength);
        if (email is not null && (!email.Contains('@', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal)))
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceEmailInvalid, "Invalid email.");
        RecipientEmail = email;
        Subject = InvoiceSettings.Text(data.Subject, MaxSubjectLength);
        Notes = InvoiceSettings.Text(data.Notes, MaxNotesLength);
        ServicePeriod = InvoiceSettings.Text(data.ServicePeriod, MaxServicePeriodLength);

        if (data.DueDate < data.InvoiceDate)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceDueDateInvalid, "The due date is before the invoice date.");
        InvoiceDate = data.InvoiceDate;
        DueDate = data.DueDate;

        if (data.Lines.Count is 0 or > MaxLines)
            throw new PreconditionException(AccountingErrorCode.AccountingLinesInvalid, [MaxLines.ToString(CultureInfo.InvariantCulture)], "An invoice has 1 to 30 lines.");
        lines.Clear();
        var position = 0;
        foreach (var line in data.Lines)
            lines.Add(new InvoiceLine(++position, line));

        var total = lines.Sum(l => (long)l.TotalCents);
        if (total <= 0 || total > AccountingEntry.MaxAmountCents)
            throw new PreconditionException(AccountingErrorCode.AccountingAmountInvalid, [(AccountingEntry.MaxAmountCents / 100).ToString(CultureInfo.InvariantCulture)], "Invalid total.");
        TotalCents = (int)total;
    }
}

public class InvoiceLine
{
    public const int MaxTextLength = 200;

#pragma warning disable CS8618
    private InvoiceLine() { }
#pragma warning restore CS8618

    internal InvoiceLine(int position, Invoice.LineData data)
    {
        var text = InvoiceSettings.Text(data.Text, MaxTextLength, required: true)!;
        if (data.Quantity <= 0 || data.Quantity > 100_000 || decimal.Round(data.Quantity, 2) != data.Quantity)
            throw new PreconditionException(AccountingErrorCode.AccountingInvoiceQuantityInvalid, "Invalid quantity.");
        if (Math.Abs((long)data.UnitPriceCents) > AccountingEntry.MaxAmountCents)
            throw new PreconditionException(AccountingErrorCode.AccountingAmountInvalid, [(AccountingEntry.MaxAmountCents / 100).ToString(CultureInfo.InvariantCulture)], "Invalid price.");

        Position = position;
        Text = text;
        Quantity = data.Quantity;
        UnitPriceCents = data.UnitPriceCents;
        CategoryId = data.CategoryId;
        CostCenterId = data.CostCenterId;
        var total = Math.Round(data.Quantity * data.UnitPriceCents, MidpointRounding.AwayFromZero);
        if (Math.Abs(total) > AccountingEntry.MaxAmountCents)
            throw new PreconditionException(AccountingErrorCode.AccountingAmountInvalid, [(AccountingEntry.MaxAmountCents / 100).ToString(CultureInfo.InvariantCulture)], "Invalid line total.");
        TotalCents = (int)total;
    }

    public int Id { get; private set; }
    public int InvoiceId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; }
    public decimal Quantity { get; private set; }
    public int UnitPriceCents { get; private set; }

    /// <summary>A negative line is a discount.</summary>
    public int TotalCents { get; private set; }

    public int CategoryId { get; private set; }
    public int? CostCenterId { get; private set; }
}

/// <summary>
/// The EPC QR code (GiroCode, "Zahlen mit Code" in Austria) of a credit transfer: banking apps read beneficiary, IBAN, amount and reference
/// from it. Version 002 (no BIC needed in the EEA), UTF-8, the reference as unstructured text.
/// </summary>
public static class EpcQrPayload
{
    public static string For(string name, string iban, string? bic, int amountCents, string reference)
    {
        static string Clean(string value, int max)
        {
            var text = string.Join(' ', value.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)).Trim();
            return text.Length > max ? text[..max] : text;
        }

        var amount = (amountCents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        return string.Join('\n',
            "BCD",
            "002",
            "1",
            "SCT",
            bic ?? "",
            Clean(name, 70),
            iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant(),
            "EUR" + amount,
            "",
            "",
            Clean(reference, 140));
    }
}
