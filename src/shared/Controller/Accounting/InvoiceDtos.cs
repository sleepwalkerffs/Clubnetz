namespace Bookennis.Shared.Controller.Accounting;

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.InvoiceStatus</c>.</summary>
public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Cancelled = 3,
}

/// <summary>What every invoice says about the club.</summary>
public record InvoiceSettingsDto
{
    /// <summary>Invoices can be issued: address and bank account are set.</summary>
    public required bool IsSetUp { get; init; }

    public string? Address { get; init; }
    public string? ZvrNumber { get; init; }
    public string? Contact { get; init; }
    public int? MoneyAccountId { get; init; }
    public string? Bic { get; init; }
    public string NumberPrefix { get; init; } = "RE";
    public int PaymentTermDays { get; init; } = 14;
    public string? Footer { get; init; }
}

/// <summary>Body of <c>PUT Accounting/invoice-settings</c>.</summary>
public record SaveInvoiceSettingsModel
{
    public string? Address { get; init; }
    public string? ZvrNumber { get; init; }
    public string? Contact { get; init; }
    public int MoneyAccountId { get; init; }
    public string? Bic { get; init; }
    public string? NumberPrefix { get; init; }
    public int PaymentTermDays { get; init; } = 14;
    public string? Footer { get; init; }
}

public record InvoiceDto
{
    public required int Id { get; init; }

    /// <summary><c>null</c> for a draft.</summary>
    public string? Number { get; init; }

    public required InvoiceStatus Status { get; init; }
    public required string RecipientName { get; init; }
    public string? RecipientEmail { get; init; }
    public string? Subject { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required DateOnly DueDate { get; init; }
    public required int TotalCents { get; init; }

    /// <summary>Issued, unpaid and past its due date.</summary>
    public required bool IsOverdue { get; init; }

    public required int ReminderCount { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public DateOnly? PaidDate { get; init; }
    public int? EntryId { get; init; }
}

public record InvoiceDetailDto : InvoiceDto
{
    public int? RecipientMemberId { get; init; }
    public string? RecipientAddress { get; init; }
    public string? Notes { get; init; }
    public string? ServicePeriod { get; init; }
    public List<InvoiceLineDto> Lines { get; init; } = [];
    public string? SentTo { get; init; }
    public DateTimeOffset? LastReminderAt { get; init; }
    public string? CancelReason { get; init; }

    /// <summary>The number of the income entry that books the payment.</summary>
    public int? EntryNumber { get; init; }
}

public record InvoiceLineDto
{
    public required string Text { get; init; }
    public required decimal Quantity { get; init; }
    public required int UnitPriceCents { get; init; }
    public required int TotalCents { get; init; }
    public required int CategoryId { get; init; }
    public int? CostCenterId { get; init; }
}

public record GetInvoicesResult
{
    public List<InvoiceDto> Invoices { get; init; } = [];

    /// <summary>Issued and unpaid (of all years).</summary>
    public required int OpenCount { get; init; }

    public required long OpenCents { get; init; }
    public required int OverdueCount { get; init; }
}

/// <summary>Body of <c>POST|PUT Accounting/invoices</c>.</summary>
public record SaveInvoiceModel
{
    public int? RecipientMemberId { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientAddress { get; init; }
    public string? RecipientEmail { get; init; }
    public string? Subject { get; init; }
    public string? Notes { get; init; }
    public string? ServicePeriod { get; init; }
    public DateOnly InvoiceDate { get; init; }
    public DateOnly DueDate { get; init; }
    public List<SaveInvoiceLineModel> Lines { get; init; } = [];
}

public record SaveInvoiceLineModel
{
    public string? Text { get; init; }
    public decimal Quantity { get; init; } = 1;
    public int UnitPriceCents { get; init; }
    public int CategoryId { get; init; }
    public int? CostCenterId { get; init; }
}

/// <summary>Body of <c>POST Accounting/invoices/{id}/pay</c>: the money came in on this account on this day.</summary>
public record PayInvoiceModel
{
    public int MoneyAccountId { get; init; }
    public DateOnly Date { get; init; }
}

public record CancelInvoiceModel
{
    public string? Reason { get; init; }
}

/// <summary>Body of <c>POST Accounting/invoices/{id}/send|remind</c>: another address than the one on the invoice.</summary>
public record SendInvoiceModel
{
    public string? Email { get; init; }
}
