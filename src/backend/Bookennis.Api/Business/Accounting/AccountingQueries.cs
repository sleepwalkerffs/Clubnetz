using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using AccountingEntryKind = Bookennis.Domain.Accounting.AccountingEntryKind;
using AccountingEntryStatus = Bookennis.Domain.Accounting.AccountingEntryStatus;

namespace Bookennis.Api.Business.Accounting;

internal static class AccountingQueries
{
    private const long LockNamespace = 0x4143434FL << 32; // "ACCO"

    /// <summary>
    /// Everything that numbers entries, changes balances or closes years runs one after the other per club: two entries never get the
    /// same number and a year is never closed while an entry is saved into it. Held until the transaction of the request ends.
    /// </summary>
    public static Task LockAccounting(this AppDbContext context, int clubId, CancellationToken cancellationToken)
    {
        var key = LockNamespace + clubId;
        return context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    public static async Task<AccountingSettings> GetAccountingSettings(this AppDbContext context, int clubId, CancellationToken cancellationToken)
        => await context.AccountingSettings.SingleOrDefaultAsync(s => s.ClubId == clubId, cancellationToken)
           ?? throw new PreconditionException(AccountingErrorCode.AccountingNotSetUp, "The accounting is not set up.");

    public static Task<FiscalYear> GetFiscalYear(this AppDbContext context, int clubId, int fiscalYearId, CancellationToken cancellationToken)
        => context.FiscalYears.Include(y => y.OpeningBalances).SingleRequiredAsync(y => y.Id == fiscalYearId && y.ClubId == clubId, cancellationToken);

    /// <summary>The year that ends the day before <paramref name="year"/> starts, if the club has it in Clubnetz.</summary>
    public static Task<FiscalYear?> GetPreviousFiscalYear(this AppDbContext context, FiscalYear year, CancellationToken cancellationToken)
        => context.FiscalYears.Include(y => y.OpeningBalances)
            .SingleOrDefaultAsync(y => y.ClubId == year.ClubId && y.EndDate == year.StartDate.AddDays(-1), cancellationToken);

    public static Task<FiscalYear?> GetNextFiscalYear(this AppDbContext context, FiscalYear year, CancellationToken cancellationToken)
        => context.FiscalYears.Include(y => y.OpeningBalances)
            .SingleOrDefaultAsync(y => y.ClubId == year.ClubId && y.StartDate == year.EndDate.AddDays(1), cancellationToken);

    /// <summary>The local day of the club (Austria): what "today" means for fiscal years.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(ClubTimeZone.ToLocal(now));

    /// <summary>"First Last" of members of the club, for the names shown in the accounting (never more than the name).</summary>
    public static async Task<Dictionary<int, string>> MemberNames(this AppDbContext context, int clubId, IEnumerable<int?> memberIds, CancellationToken cancellationToken)
    {
        var ids = memberIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await (
            from member in context.Set<Member>()
            join user in context.Users on member.UserId equals user.Id
            where ids.Contains(member.Id) && member.ClubId == clubId
            select new { member.Id, user.FirstName, user.LastName }
        ).ToDictionaryAsync(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim(), cancellationToken);
    }

    public static FiscalYearDto ToDto(this FiscalYear year, IReadOnlyDictionary<int, string> names) => new()
    {
        Id = year.Id,
        Label = year.Label,
        StartDate = year.StartDate,
        EndDate = year.EndDate,
        Status = (global::Bookennis.Shared.Controller.Accounting.FiscalYearStatus)year.Status,
        ClosedAt = year.ClosedAt,
        ClosedByName = year.ClosedByMemberId is { } closedBy ? names.GetValueOrDefault(closedBy) : null,
        OpeningBalancesCarried = year.OpeningBalancesCarried,
        OpeningBalances = year.OpeningBalances.Select(b => new OpeningBalanceDto { MoneyAccountId = b.MoneyAccountId, AmountCents = b.AmountCents, IsCarried = b.IsCarried }).ToList(),
        AuditStatus = (global::Bookennis.Shared.Controller.Accounting.FiscalYearAuditStatus)year.AuditStatus,
        AuditedAt = year.AuditedAt,
        AuditedByName = year.AuditedByMemberId is { } auditedBy ? names.GetValueOrDefault(auditedBy) : null,
        AuditComment = year.AuditComment,
    };

    public static AccountingEntryDto ToDto(this AccountingEntry entry, Func<int, AccountingCategoryKind> kindOfCategory, int receiptCount) => new()
    {
        Id = entry.Id,
        FiscalYearId = entry.FiscalYearId,
        Number = entry.Number,
        Date = entry.Date,
        Text = entry.Text,
        Kind = (global::Bookennis.Shared.Controller.Accounting.AccountingEntryKind)entry.Kind,
        MoneyAccountId = entry.MoneyAccountId,
        ToMoneyAccountId = entry.ToMoneyAccountId,
        TransferAmountCents = entry.TransferAmountCents,
        AmountCents = entry.Kind == AccountingEntryKind.Transfer ? entry.TransferAmountCents ?? 0 : NetCents(entry, kindOfCategory),
        CounterpartyMemberId = entry.CounterpartyMemberId,
        CounterpartyName = entry.CounterpartyName,
        Status = (global::Bookennis.Shared.Controller.Accounting.AccountingEntryStatus)entry.Status,
        CancelledAt = entry.CancelledAt,
        CancelReason = entry.CancelReason,
        Lines = entry.Lines.OrderBy(l => l.Position).Select(l => new AccountingEntryLineDto { CategoryId = l.CategoryId, CostCenterId = l.CostCenterId, AmountCents = l.AmountCents }).ToList(),
        ReceiptCount = receiptCount,
    };

    /// <summary>Income minus expenses of the lines (also for a cancelled entry, so the list can show what it was).</summary>
    public static long NetCents(AccountingEntry entry, Func<int, AccountingCategoryKind> kindOfCategory)
        => entry.Lines.Sum(l => kindOfCategory(l.CategoryId) == AccountingCategoryKind.Income ? (long)l.AmountCents : -(long)l.AmountCents);

    public static AccountingReceiptDto ToDto(this AccountingReceipt receipt, int? entryNumber, IReadOnlyDictionary<int, string> names) => new()
    {
        Id = receipt.Id,
        EntryId = receipt.EntryId,
        EntryNumber = entryNumber,
        FileName = receipt.FileName,
        ContentType = receipt.ContentType,
        Size = receipt.Size,
        UploadedAt = receipt.UploadedAt,
        UploadedByName = receipt.UploadedByMemberId is { } by ? names.GetValueOrDefault(by) : null,
    };

    /// <summary>The kind (income/expense) of every category of the club, to sum up lines.</summary>
    public static Task<Dictionary<int, AccountingCategoryKind>> CategoryKinds(this AppDbContext context, int clubId, CancellationToken cancellationToken)
        => context.AccountingCategories.Where(c => c.ClubId == clubId).ToDictionaryAsync(c => c.Id, c => c.Kind, cancellationToken);

    /// <summary>The active entries of a fiscal year with their lines, oldest first.</summary>
    public static Task<List<AccountingEntry>> ActiveEntries(this AppDbContext context, int clubId, int fiscalYearId, CancellationToken cancellationToken)
        => context.AccountingEntries.AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.ClubId == clubId && e.FiscalYearId == fiscalYearId && e.Status == AccountingEntryStatus.Active)
            .OrderBy(e => e.Date).ThenBy(e => e.Number)
            .ToListAsync(cancellationToken);
}
