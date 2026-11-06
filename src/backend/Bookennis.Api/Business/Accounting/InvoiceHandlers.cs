using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.Email;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using InvoiceStatus = Bookennis.Domain.Accounting.InvoiceStatus;

namespace Bookennis.Api.Business.Accounting;

public record GetInvoiceSettings(int ClubId) : IQuery<InvoiceSettingsDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetInvoiceSettings, InvoiceSettingsDto>
    {
        public async Task<InvoiceSettingsDto> Handle(GetInvoiceSettings request, CancellationToken cancellationToken)
        {
            var settings = await context.InvoiceSettings.AsNoTracking().SingleOrDefaultAsync(s => s.ClubId == request.ClubId, cancellationToken);
            return settings is null
                ? new InvoiceSettingsDto { IsSetUp = false }
                : new InvoiceSettingsDto
                {
                    IsSetUp = true,
                    Address = settings.Address,
                    ZvrNumber = settings.ZvrNumber,
                    Contact = settings.Contact,
                    MoneyAccountId = settings.MoneyAccountId,
                    Bic = settings.Bic,
                    NumberPrefix = settings.NumberPrefix,
                    PaymentTermDays = settings.PaymentTermDays,
                    Footer = settings.Footer,
                };
        }
    }
}

/// <summary>The club's details for invoices. The money account is one of the club with an IBAN: it is printed with the QR code.</summary>
public record SaveInvoiceSettings(int ClubId, SaveInvoiceSettingsModel Model) : ICommand<InvoiceSettingsDto>
{
    public class Handler(AppDbContext context, IMediator mediator) : IRequestHandler<SaveInvoiceSettings, InvoiceSettingsDto>
    {
        public async Task<InvoiceSettingsDto> Handle(SaveInvoiceSettings request, CancellationToken cancellationToken)
        {
            await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var model = request.Model;
            var account = await context.MoneyAccounts.SingleRequiredAsync(a => a.Id == model.MoneyAccountId && a.ClubId == request.ClubId, cancellationToken);
            if (account.Iban is null)
                throw new PreconditionException(AccountingErrorCode.AccountingInvoiceAccountWithoutIban, "The money account has no IBAN.");

            var data = new InvoiceSettings.SettingsData(model.Address, model.ZvrNumber, model.Contact, account.Id, model.Bic, model.NumberPrefix, model.PaymentTermDays, model.Footer);
            var settings = await context.InvoiceSettings.SingleOrDefaultAsync(s => s.ClubId == request.ClubId, cancellationToken);
            if (settings is null)
                context.Add(new InvoiceSettings(request.ClubId, data));
            else
                settings.Change(data);

            await context.SaveChangesAsync(cancellationToken);
            return await mediator.Send(new GetInvoiceSettings(request.ClubId), cancellationToken);
        }
    }
}

/// <summary>The invoices of a status (all with <c>null</c>), newest first, and what is still open.</summary>
public record GetInvoices(int ClubId, InvoiceStatus? Status, DateTimeOffset Now) : IQuery<GetInvoicesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetInvoices, GetInvoicesResult>
    {
        public async Task<GetInvoicesResult> Handle(GetInvoices request, CancellationToken cancellationToken)
        {
            var today = AccountingQueries.Today(request.Now);
            var invoices = await context.Invoices.AsNoTracking()
                .Where(i => i.ClubId == request.ClubId && (request.Status == null || i.Status == request.Status))
                .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
                .Take(500)
                .ToListAsync(cancellationToken);
            var open = await context.Invoices.AsNoTracking()
                .Where(i => i.ClubId == request.ClubId && i.Status == InvoiceStatus.Issued)
                .Select(i => new { i.TotalCents, i.DueDate })
                .ToListAsync(cancellationToken);

            return new GetInvoicesResult
            {
                Invoices = invoices.Select(i => InvoiceMapper.ToDto(i, today)).ToList(),
                OpenCount = open.Count,
                OpenCents = open.Sum(i => (long)i.TotalCents),
                OverdueCount = open.Count(i => today > i.DueDate),
            };
        }
    }
}

public record GetInvoice(int ClubId, int InvoiceId, DateTimeOffset Now) : IQuery<InvoiceDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetInvoice, InvoiceDetailDto>
    {
        public async Task<InvoiceDetailDto> Handle(GetInvoice request, CancellationToken cancellationToken)
            => await InvoiceMapper.Detail(context, await context.Invoices.AsNoTracking().Include(i => i.Lines)
                .SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken), request.Now, cancellationToken);
    }
}

/// <summary>A new draft, in the language of the treasurer. Categories (income) and cost centers of the club, the member of the club.</summary>
public record CreateInvoice(int ClubId, SaveInvoiceModel Model, DateTimeOffset Now) : ICommand<InvoiceDetailDto>
{
    public class Handler(AppDbContext context, IUserLanguageAccessor languageAccessor) : IRequestHandler<CreateInvoice, InvoiceDetailDto>
    {
        public async Task<InvoiceDetailDto> Handle(CreateInvoice request, CancellationToken cancellationToken)
        {
            await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var data = await InvoiceMapper.Check(context, request.ClubId, request.Model, null, cancellationToken);
            var invoice = new Invoice(request.ClubId, data, languageAccessor.GetCultureInfo().TwoLetterISOLanguageName == "de");
            context.Add(invoice);
            await context.SaveChangesAsync(cancellationToken);
            return await InvoiceMapper.Detail(context, invoice, request.Now, cancellationToken);
        }
    }
}

/// <summary>Changes a draft (an issued invoice is final).</summary>
public record UpdateInvoice(int ClubId, int InvoiceId, SaveInvoiceModel Model, DateTimeOffset Now) : ICommand<InvoiceDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateInvoice, InvoiceDetailDto>
    {
        public async Task<InvoiceDetailDto> Handle(UpdateInvoice request, CancellationToken cancellationToken)
        {
            var invoice = await context.Invoices.Include(i => i.Lines).SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken);
            invoice.EnsureDraft();
            invoice.Update(await InvoiceMapper.Check(context, request.ClubId, request.Model, invoice, cancellationToken));
            await context.SaveChangesAsync(cancellationToken);
            return await InvoiceMapper.Detail(context, invoice, request.Now, cancellationToken);
        }
    }
}

/// <summary>Deletes a draft (it never had a number).</summary>
public record DeleteInvoice(int ClubId, int InvoiceId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteInvoice>
    {
        public async Task<Unit> Handle(DeleteInvoice request, CancellationToken cancellationToken)
        {
            var invoice = await context.Invoices.Include(i => i.Lines).SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken);
            invoice.EnsureDraft();
            context.Remove(invoice);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>Issues a draft: it gets the next number of the year of its date (under the accounting lock: never twice) and is final.</summary>
public record IssueInvoice(int ClubId, int InvoiceId, DateTimeOffset Now) : ICommand<InvoiceDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<IssueInvoice, InvoiceDetailDto>
    {
        public async Task<InvoiceDetailDto> Handle(IssueInvoice request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var settings = await InvoiceMapper.GetSettings(context, request.ClubId, cancellationToken);
            var invoice = await context.Invoices.Include(i => i.Lines).SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken);
            invoice.EnsureDraft();

            var year = invoice.InvoiceDate.Year;
            var last = await context.Invoices.Where(i => i.ClubId == request.ClubId && i.NumberYear == year).MaxAsync(i => i.NumberSequence, cancellationToken);
            invoice.Issue(settings, (last ?? 0) + 1, request.Now);
            await context.SaveChangesAsync(cancellationToken);
            return await InvoiceMapper.Detail(context, invoice, request.Now, cancellationToken);
        }
    }
}

/// <summary>The money came in: an income entry with the lines of the invoice (by category and cost center) on the account and day given.</summary>
public record PayInvoice(int ClubId, int UserId, int InvoiceId, PayInvoiceModel Model, DateTimeOffset Now) : ICommand<InvoiceDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<PayInvoice, InvoiceDetailDto>
    {
        public async Task<InvoiceDetailDto> Handle(PayInvoice request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var invoice = await context.Invoices.Include(i => i.Lines).SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken);
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            await InvoicePayment.Pay(context, invoice, request.Model.MoneyAccountId, request.Model.Date, memberId, request.Now, cancellationToken);
            return await InvoiceMapper.Detail(context, invoice, request.Now, cancellationToken);
        }
    }
}

/// <summary>Storno of an issued, unpaid invoice: it keeps its number.</summary>
public record CancelInvoice(int ClubId, int InvoiceId, string? Reason, DateTimeOffset Now) : ICommand<InvoiceDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CancelInvoice, InvoiceDetailDto>
    {
        public async Task<InvoiceDetailDto> Handle(CancelInvoice request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var invoice = await context.Invoices.Include(i => i.Lines).SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken);
            invoice.Cancel(request.Reason, request.Now);
            await context.SaveChangesAsync(cancellationToken);
            return await InvoiceMapper.Detail(context, invoice, request.Now, cancellationToken);
        }
    }
}

/// <summary>The PDF of an invoice (a draft is marked as such).</summary>
public record GetInvoicePdf(int ClubId, int InvoiceId) : IQuery<InvoicePdfFile>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetInvoicePdf, InvoicePdfFile>
    {
        public async Task<InvoicePdfFile> Handle(GetInvoicePdf request, CancellationToken cancellationToken)
        {
            var invoice = await context.Invoices.AsNoTracking().Include(i => i.Lines)
                .SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken);
            return new InvoicePdfFile(InvoicePdf.Render(await InvoiceMapper.PdfData(context, invoice, cancellationToken)), InvoiceMapper.FileName(invoice));
        }
    }
}

public record InvoicePdfFile(byte[] Data, string FileName)
{
    public const string ContentType = "application/pdf";
}

/// <summary>
/// Emails an issued invoice with its PDF to its recipient (or <paramref name="Email"/>), as the club email <c>Invoice</c>; with
/// <paramref name="Reminder"/> as <c>InvoiceReminder</c> to an open invoice, counting the reminders.
/// </summary>
public record SendInvoice(int ClubId, int InvoiceId, string? Email, bool Reminder, DateTimeOffset Now) : ICommand<InvoiceDetailDto>
{
    public class Handler(AppDbContext context, IMediator mediator) : IRequestHandler<SendInvoice, InvoiceDetailDto>
    {
        public async Task<InvoiceDetailDto> Handle(SendInvoice request, CancellationToken cancellationToken)
        {
            var invoice = await context.Invoices.Include(i => i.Lines).SingleRequiredAsync(i => i.Id == request.InvoiceId && i.ClubId == request.ClubId, cancellationToken);
            var email = string.IsNullOrWhiteSpace(request.Email) ? invoice.RecipientEmail : request.Email.Trim();
            if (email is null)
                throw new PreconditionException(AccountingErrorCode.AccountingInvoiceNoEmail, "The invoice has no email address.");
            if (!email.Contains('@', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal) || email.Length > Invoice.MaxEmailLength)
                throw new PreconditionException(AccountingErrorCode.AccountingInvoiceEmailInvalid, "Invalid email.");

            if (request.Reminder)
                invoice.RecordReminder(request.Now);
            else
                invoice.RecordSent(email, request.Now);

            var data = await InvoiceMapper.PdfData(context, invoice, cancellationToken);
            var language = invoice.German ? Language.German : Language.English;
            var culture = language.ToCultureInfo();
            var variables = new InvoiceEmailVariables(
                new MemberVariables(invoice.RecipientName, ""),
                new InvoiceVariables(invoice.Number!, (invoice.TotalCents / 100m).ToString("C", culture), invoice.InvoiceDate.ToString("d", culture),
                    invoice.DueDate.ToString("d", culture), invoice.Subject, data.Iban, invoice.ReminderCount));
            await context.SaveChangesAsync(cancellationToken);

            await mediator.Send(new SendClubEmail(request.ClubId, request.Reminder ? ClubEmailType.InvoiceReminder : ClubEmailType.Invoice, email, invoice.RecipientName, language,
                variables, Attachments: [new Attachment(InvoiceMapper.FileName(invoice), InvoiceAttachmentResolver.CreateUri(invoice.Id))]), cancellationToken);
            return await InvoiceMapper.Detail(context, invoice, request.Now, cancellationToken);
        }
    }
}

/// <summary>The PDF of an invoice email is drawn when the email is sent: the email job only carries <c>accounting-invoice:{id}</c>.</summary>
public class InvoiceAttachmentResolver(AppDbContext context) : IEmailAttachmentResolver
{
    public const string Scheme = "accounting-invoice";

    public static Uri CreateUri(int invoiceId) => new($"{Scheme}:{invoiceId.ToString(CultureInfo.InvariantCulture)}");

    public bool Supports(Uri uri) => uri.Scheme == Scheme;

    public async Task<Stream> GetAttachmentAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!int.TryParse(uri.AbsolutePath, NumberStyles.None, CultureInfo.InvariantCulture, out var invoiceId))
            throw new ArgumentException($"'{uri}' is not a valid invoice reference.", nameof(uri));

        // The email job runs without a tenant: the id alone names the invoice
        var invoice = await context.Invoices.IgnoreQueryFilters().AsNoTracking().Include(i => i.Lines).SingleOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
                      ?? throw new EntityNotFoundException(typeof(Invoice), invoiceId);
        return new MemoryStream(InvoicePdf.Render(await InvoiceMapper.PdfData(context, invoice, cancellationToken, ignoreFilters: true)), writable: false);
    }
}

internal static class InvoicePayment
{
    /// <summary>Books the payment of <paramref name="invoice"/> as an income entry and marks it paid. The caller holds the accounting lock.</summary>
    public static async Task<AccountingEntry> Pay(AppDbContext context, Invoice invoice, int moneyAccountId, DateOnly date, int? memberId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        invoice.EnsureOpen();
        var model = new SaveAccountingEntryModel
        {
            Date = date,
            Text = string.Join(" ", new[] { invoice.German ? "Rechnung" : "Invoice", invoice.Number, invoice.Subject }.Where(t => !string.IsNullOrWhiteSpace(t))),
            Kind = global::Bookennis.Shared.Controller.Accounting.AccountingEntryKind.Booking,
            MoneyAccountId = moneyAccountId,
            Lines = invoice.Lines
                .GroupBy(l => (l.CategoryId, l.CostCenterId))
                .Select(g => new SaveAccountingEntryLineModel { CategoryId = g.Key.CategoryId, CostCenterId = g.Key.CostCenterId, AmountCents = g.Sum(l => l.TotalCents) })
                .Where(l => l.AmountCents != 0)
                .ToList(),
            CounterpartyMemberId = invoice.RecipientMemberId,
            CounterpartyName = invoice.RecipientName,
        };
        if (model.Text!.Length > AccountingEntry.MaxTextLength)
            model = model with { Text = model.Text[..AccountingEntry.MaxTextLength] };

        var entry = await AccountingEntryCreation.Create(context, invoice.ClubId, memberId, model, now, cancellationToken);
        invoice.MarkPaid(entry, date);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }
}

internal static class InvoiceMapper
{
    public static async Task<InvoiceSettings> GetSettings(AppDbContext context, int clubId, CancellationToken cancellationToken)
        => await context.InvoiceSettings.SingleOrDefaultAsync(s => s.ClubId == clubId, cancellationToken)
           ?? throw new PreconditionException(AccountingErrorCode.AccountingInvoicingNotSetUp, "The details for invoices are not set.");

    /// <summary>Lines with income categories and cost centers of the club, a member of the club.</summary>
    public static async Task<Invoice.InvoiceData> Check(AppDbContext context, int clubId, SaveInvoiceModel model, Invoice? existing, CancellationToken cancellationToken)
    {
        var lines = model.Lines ?? [];
        var categoryIds = lines.Select(l => l.CategoryId).Distinct().ToList();
        var categories = await context.AccountingCategories.Where(c => c.ClubId == clubId && categoryIds.Contains(c.Id)).ToListAsync(cancellationToken);
        if (categories.Count != categoryIds.Count || categories.Any(c => c.Kind != AccountingCategoryKind.Income))
            throw new EntityNotFoundException(typeof(AccountingCategory));
        foreach (var category in categories.Where(c => existing is null || existing.Lines.All(l => l.CategoryId != c.Id)))
            category.EnsureUsable();

        var costCenterIds = lines.Where(l => l.CostCenterId is not null).Select(l => l.CostCenterId!.Value).Distinct().ToList();
        var costCenters = await context.CostCenters.Where(c => c.ClubId == clubId && costCenterIds.Contains(c.Id)).ToListAsync(cancellationToken);
        if (costCenters.Count != costCenterIds.Count)
            throw new EntityNotFoundException(typeof(CostCenter));

        if (model.RecipientMemberId is { } memberId && !await context.ClubMembers.AnyAsync(m => m.Id == memberId && m.ClubId == clubId, cancellationToken))
            throw new EntityNotFoundException(typeof(Domain.Members.ClubMember), memberId);

        return new Invoice.InvoiceData(model.RecipientMemberId, model.RecipientName, model.RecipientAddress, model.RecipientEmail, model.Subject, model.Notes, model.ServicePeriod,
            model.InvoiceDate, model.DueDate, lines.Select(l => new Invoice.LineData(l.Text, l.Quantity, l.UnitPriceCents, l.CategoryId, l.CostCenterId)).ToList());
    }

    public static async Task<InvoicePdfData> PdfData(AppDbContext context, Invoice invoice, CancellationToken cancellationToken, bool ignoreFilters = false)
    {
        var settingsQuery = ignoreFilters ? context.InvoiceSettings.IgnoreQueryFilters() : context.InvoiceSettings;
        var settings = await settingsQuery.AsNoTracking().SingleOrDefaultAsync(s => s.ClubId == invoice.ClubId, cancellationToken)
                       ?? throw new PreconditionException(AccountingErrorCode.AccountingInvoicingNotSetUp, "The details for invoices are not set.");
        var clubs = ignoreFilters ? context.Clubs.IgnoreQueryFilters() : context.Clubs;
        var clubName = await clubs.Where(c => c.Id == invoice.ClubId).Select(c => c.Name).SingleAsync(cancellationToken);
        var accounts = ignoreFilters ? context.MoneyAccounts.IgnoreQueryFilters() : context.MoneyAccounts;
        var iban = await accounts.Where(a => a.Id == settings.MoneyAccountId && a.ClubId == invoice.ClubId).Select(a => a.Iban).SingleAsync(cancellationToken)
                   ?? throw new PreconditionException(AccountingErrorCode.AccountingInvoiceAccountWithoutIban, "The money account has no IBAN.");
        return new InvoicePdfData(invoice, settings, clubName, iban);
    }

    public static string FileName(Invoice invoice)
        => $"{(invoice.German ? "Rechnung" : "Invoice")}-{invoice.Number ?? (invoice.German ? "Entwurf" : "Draft")}.pdf";

    public static InvoiceDto ToDto(Invoice invoice, DateOnly today) => new()
    {
        Id = invoice.Id,
        Number = invoice.Number,
        Status = (global::Bookennis.Shared.Controller.Accounting.InvoiceStatus)invoice.Status,
        RecipientName = invoice.RecipientName,
        RecipientEmail = invoice.RecipientEmail,
        Subject = invoice.Subject,
        InvoiceDate = invoice.InvoiceDate,
        DueDate = invoice.DueDate,
        TotalCents = invoice.TotalCents,
        IsOverdue = invoice.IsOverdue(today),
        ReminderCount = invoice.ReminderCount,
        SentAt = invoice.SentAt,
        PaidDate = invoice.PaidDate,
        EntryId = invoice.EntryId,
    };

    public static async Task<InvoiceDetailDto> Detail(AppDbContext context, Invoice invoice, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entryNumber = invoice.EntryId is { } entryId ? await context.AccountingEntries.Where(e => e.Id == entryId).Select(e => (int?)e.Number).SingleOrDefaultAsync(cancellationToken) : null;
        var dto = ToDto(invoice, AccountingQueries.Today(now));
        return new InvoiceDetailDto
        {
            Id = dto.Id,
            Number = dto.Number,
            Status = dto.Status,
            RecipientName = dto.RecipientName,
            RecipientEmail = dto.RecipientEmail,
            Subject = dto.Subject,
            InvoiceDate = dto.InvoiceDate,
            DueDate = dto.DueDate,
            TotalCents = dto.TotalCents,
            IsOverdue = dto.IsOverdue,
            ReminderCount = dto.ReminderCount,
            SentAt = dto.SentAt,
            PaidDate = dto.PaidDate,
            EntryId = dto.EntryId,
            RecipientMemberId = invoice.RecipientMemberId,
            RecipientAddress = invoice.RecipientAddress,
            Notes = invoice.Notes,
            ServicePeriod = invoice.ServicePeriod,
            Lines = invoice.Lines.OrderBy(l => l.Position).Select(l => new InvoiceLineDto
            {
                Text = l.Text,
                Quantity = l.Quantity,
                UnitPriceCents = l.UnitPriceCents,
                TotalCents = l.TotalCents,
                CategoryId = l.CategoryId,
                CostCenterId = l.CostCenterId,
            }).ToList(),
            SentTo = invoice.SentTo,
            LastReminderAt = invoice.LastReminderAt,
            CancelReason = invoice.CancelReason,
            EntryNumber = entryNumber,
        };
    }
}
