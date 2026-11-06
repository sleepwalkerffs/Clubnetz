using System.Globalization;
using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using AccountingEntryKind = Bookennis.Domain.Accounting.AccountingEntryKind;
using AccountingEntryStatus = Bookennis.Domain.Accounting.AccountingEntryStatus;

namespace Bookennis.Api.Business.Accounting;

/// <summary>The entries of a fiscal year, newest first, filtered by money account, category, cost center and text.</summary>
public record GetAccountingEntries(int ClubId, int FiscalYearId, int? MoneyAccountId, int? CategoryId, int? CostCenterId, string? Search, bool IncludeCancelled)
    : IQuery<GetAccountingEntriesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingEntries, GetAccountingEntriesResult>
    {
        public async Task<GetAccountingEntriesResult> Handle(GetAccountingEntries request, CancellationToken cancellationToken)
        {
            var year = await context.FiscalYears.AsNoTracking().SingleRequiredAsync(y => y.Id == request.FiscalYearId && y.ClubId == request.ClubId, cancellationToken);

            var query = context.AccountingEntries.AsNoTracking().Include(e => e.Lines)
                .Where(e => e.ClubId == request.ClubId && e.FiscalYearId == year.Id);
            if (!request.IncludeCancelled)
                query = query.Where(e => e.Status == AccountingEntryStatus.Active);
            if (request.MoneyAccountId is { } accountId)
                query = query.Where(e => e.MoneyAccountId == accountId || e.ToMoneyAccountId == accountId);
            if (request.CategoryId is { } categoryId)
                query = query.Where(e => e.Lines.Any(l => l.CategoryId == categoryId));
            if (request.CostCenterId is { } costCenterId)
                query = query.Where(e => e.Lines.Any(l => l.CostCenterId == costCenterId));
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                var number = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (int?)null;
                var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(e => EF.Functions.ILike(e.Text, pattern) || (e.CounterpartyName != null && EF.Functions.ILike(e.CounterpartyName, pattern)) || e.Number == number);
            }

            var entries = await query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Number).ToListAsync(cancellationToken);
            var ids = entries.Select(e => e.Id).ToList();
            var receiptCounts = await context.AccountingReceipts
                .Where(r => r.ClubId == request.ClubId && r.EntryId != null && ids.Contains(r.EntryId.Value))
                .GroupBy(r => r.EntryId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            var kinds = await context.CategoryKinds(request.ClubId, cancellationToken);

            // Filtered by category or cost center, the sums only count the matching lines
            var countedLines = entries.Where(e => e.Status == AccountingEntryStatus.Active)
                .SelectMany(e => e.Lines)
                .Where(l => (request.CategoryId is null || l.CategoryId == request.CategoryId) && (request.CostCenterId is null || l.CostCenterId == request.CostCenterId))
                .ToList();

            return new GetAccountingEntriesResult
            {
                Entries = entries.Select(e => e.ToDto(id => kinds[id], receiptCounts.GetValueOrDefault(e.Id))).ToList(),
                IncomeCents = countedLines.Where(l => kinds[l.CategoryId] == AccountingCategoryKind.Income).Sum(l => (long)l.AmountCents),
                ExpenseCents = countedLines.Where(l => kinds[l.CategoryId] == AccountingCategoryKind.Expense).Sum(l => (long)l.AmountCents),
            };
        }
    }
}

/// <summary>One entry with its receipts and its history.</summary>
public record GetAccountingEntry(int ClubId, int EntryId) : IQuery<AccountingEntryDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingEntry, AccountingEntryDetailDto>
    {
        public Task<AccountingEntryDetailDto> Handle(GetAccountingEntry request, CancellationToken cancellationToken)
            => AccountingEntryDetails.Load(context, request.ClubId, request.EntryId, cancellationToken);
    }
}

/// <summary>Books an income, an expense or a transfer into the open fiscal year its date belongs to, and attaches receipts from the inbox.</summary>
public record CreateAccountingEntry(int ClubId, int UserId, SaveAccountingEntryModel Model, DateTimeOffset Now) : ICommand<AccountingEntryDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateAccountingEntry, AccountingEntryDetailDto>
    {
        public async Task<AccountingEntryDetailDto> Handle(CreateAccountingEntry request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            await context.GetAccountingSettings(request.ClubId, cancellationToken);

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var entry = await AccountingEntryCreation.Create(context, request.ClubId, memberId, request.Model, request.Now, cancellationToken);
            return await AccountingEntryDetails.Load(context, request.ClubId, entry.Id, cancellationToken);
        }
    }
}

/// <summary>Changes an entry of an open fiscal year (the date stays in that year); receipts left out go back to the inbox.</summary>
public record UpdateAccountingEntry(int ClubId, int UserId, int EntryId, SaveAccountingEntryModel Model, DateTimeOffset Now) : ICommand<AccountingEntryDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateAccountingEntry, AccountingEntryDetailDto>
    {
        public async Task<AccountingEntryDetailDto> Handle(UpdateAccountingEntry request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var entry = await context.AccountingEntries.Include(e => e.Lines).Include(e => e.Changes)
                .SingleRequiredAsync(e => e.Id == request.EntryId && e.ClubId == request.ClubId, cancellationToken);
            var year = await context.GetFiscalYear(request.ClubId, entry.FiscalYearId, cancellationToken);
            entry.EnsureChangeable(year);

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var data = await AccountingEntryInput.Check(context, request.ClubId, request.Model, entry, cancellationToken);
            var receipts = await AccountingEntryInput.Receipts(context, request.ClubId, request.Model.ReceiptIds ?? [], entry.Id, cancellationToken);

            var detached = await context.AccountingReceipts.Where(r => r.ClubId == request.ClubId && r.EntryId == entry.Id).ToListAsync(cancellationToken);
            foreach (var receipt in detached.Where(r => receipts.All(kept => kept.Id != r.Id)))
                receipt.Detach();
            foreach (var receipt in receipts)
                receipt.AttachTo(entry);

            entry.Update(year, data, receipts.Select(r => r.Id).ToList(), memberId, request.Now);
            await context.SaveChangesAsync(cancellationToken);
            await BankMapper.ReopenLinesNoLongerBooked(context, entry, cancellationToken);
            await AccountingBalances.CarryForward(context, year, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return await AccountingEntryDetails.Load(context, request.ClubId, entry.Id, cancellationToken);
        }
    }
}

/// <summary>Cancels an entry of an open fiscal year: it keeps its number and its receipts, but no longer counts.</summary>
public record CancelAccountingEntry(int ClubId, int UserId, int EntryId, string? Reason, DateTimeOffset Now) : ICommand<AccountingEntryDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CancelAccountingEntry, AccountingEntryDetailDto>
    {
        public async Task<AccountingEntryDetailDto> Handle(CancelAccountingEntry request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var entry = await context.AccountingEntries.Include(e => e.Lines).Include(e => e.Changes)
                .SingleRequiredAsync(e => e.Id == request.EntryId && e.ClubId == request.ClubId, cancellationToken);
            var year = await context.GetFiscalYear(request.ClubId, entry.FiscalYearId, cancellationToken);
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            entry.Cancel(year, request.Reason, memberId, request.Now);
            await context.SaveChangesAsync(cancellationToken);
            await BankMapper.ReopenLinesNoLongerBooked(context, entry, cancellationToken);
            await AccountingBalances.CarryForward(context, year, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return await AccountingEntryDetails.Load(context, request.ClubId, entry.Id, cancellationToken);
        }
    }
}

/// <summary>Members of the club the treasurer picks as who paid or was paid: names only, at most 20.</summary>
public record GetAccountingMembers(int ClubId, string? Search) : IQuery<List<AccountingMemberDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingMembers, List<AccountingMemberDto>>
    {
        public async Task<List<AccountingMemberDto>> Handle(GetAccountingMembers request, CancellationToken cancellationToken)
        {
            var query = from member in context.ClubMembers
                        join user in context.Users on member.UserId equals user.Id
                        where member.ClubId == request.ClubId
                        select new { member.Id, user.FirstName, user.LastName };

            foreach (var word in (request.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(3))
            {
                var pattern = $"{word.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(m => EF.Functions.ILike(m.FirstName, pattern) || EF.Functions.ILike(m.LastName, pattern));
            }

            var members = await query.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).Take(20).ToListAsync(cancellationToken);
            return members.Select(m => new AccountingMemberDto { MemberId = m.Id, Name = $"{m.FirstName} {m.LastName}".Trim() }).ToList();
        }
    }
}

/// <summary>Checks what an entry refers to before the domain gets it.</summary>
internal static class AccountingEntryInput
{
    /// <summary>
    /// The money accounts, categories and cost centers must belong to the club and must not be archived - unless the entry
    /// already used them (an archived category stays on the entries it was used for).
    /// </summary>
    public static async Task<AccountingEntry.EntryData> Check(AppDbContext context, int clubId, SaveAccountingEntryModel model, AccountingEntry? existing, CancellationToken cancellationToken,
        bool allowArchivedLines = false)
    {
        var accountIds = new[] { model.MoneyAccountId, model.ToMoneyAccountId }.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var accounts = await context.MoneyAccounts.Where(a => a.ClubId == clubId && accountIds.Contains(a.Id)).ToListAsync(cancellationToken);
        if (accounts.Count != accountIds.Count)
            throw new EntityNotFoundException(typeof(MoneyAccount));
        foreach (var account in accounts.Where(a => existing is null || (existing.MoneyAccountId != a.Id && existing.ToMoneyAccountId != a.Id)))
            account.EnsureUsable();

        var categoryIds = (model.Lines ?? []).Select(l => l.CategoryId).Distinct().ToList();
        var categories = await context.AccountingCategories.Where(c => c.ClubId == clubId && categoryIds.Contains(c.Id)).ToListAsync(cancellationToken);
        if (categories.Count != categoryIds.Count)
            throw new EntityNotFoundException(typeof(AccountingCategory));
        foreach (var category in categories.Where(c => !allowArchivedLines && (existing is null || existing.Lines.All(l => l.CategoryId != c.Id))))
            category.EnsureUsable();

        var costCenterIds = (model.Lines ?? []).Where(l => l.CostCenterId is not null).Select(l => l.CostCenterId!.Value).Distinct().ToList();
        var costCenters = await context.CostCenters.Where(c => c.ClubId == clubId && costCenterIds.Contains(c.Id)).ToListAsync(cancellationToken);
        if (costCenters.Count != costCenterIds.Count)
            throw new EntityNotFoundException(typeof(CostCenter));
        foreach (var costCenter in costCenters.Where(c => !allowArchivedLines && (existing is null || existing.Lines.All(l => l.CostCenterId != c.Id))))
            costCenter.EnsureUsable();

        var counterpartyName = model.CounterpartyName;
        if (model.CounterpartyMemberId is { } memberId)
        {
            var name = await (
                from member in context.ClubMembers
                join user in context.Users on member.UserId equals user.Id
                where member.Id == memberId && member.ClubId == clubId
                select user.FirstName + " " + user.LastName
            ).FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException(typeof(ClubMember), memberId);
            if (string.IsNullOrWhiteSpace(counterpartyName))
                counterpartyName = name;
        }

        return new AccountingEntry.EntryData(
            model.Date,
            model.Text,
            (AccountingEntryKind)model.Kind,
            model.MoneyAccountId,
            model.ToMoneyAccountId,
            model.TransferAmountCents,
            (model.Lines ?? []).Select(l => new AccountingEntry.LineData(l.CategoryId, l.CostCenterId, l.AmountCents)).ToList(),
            model.CounterpartyMemberId,
            counterpartyName);
    }

    /// <summary>The receipts to attach: of the club, in the inbox or already on this entry.</summary>
    public static async Task<List<AccountingReceipt>> Receipts(AppDbContext context, int clubId, IReadOnlyCollection<int> receiptIds, int? entryId, CancellationToken cancellationToken)
    {
        var ids = receiptIds.Distinct().ToList();
        if (ids.Count > AccountingReceipt.MaxPerEntry)
            throw new PreconditionException(AccountingErrorCode.AccountingTooManyReceipts, [AccountingReceipt.MaxPerEntry.ToString(CultureInfo.InvariantCulture)], "Too many receipts.");

        var receipts = await context.AccountingReceipts.Where(r => r.ClubId == clubId && ids.Contains(r.Id)).ToListAsync(cancellationToken);
        if (receipts.Count != ids.Count)
            throw new EntityNotFoundException(typeof(AccountingReceipt));

        var taken = receipts.FirstOrDefault(r => r.EntryId is not null && r.EntryId != entryId);
        if (taken is not null)
        {
            var number = await context.AccountingEntries.Where(e => e.Id == taken.EntryId).Select(e => e.Number).FirstAsync(cancellationToken);
            throw new PreconditionException(AccountingErrorCode.AccountingReceiptAlreadyBooked, [number.ToString(CultureInfo.InvariantCulture)], "The receipt belongs to another entry.");
        }

        return ids.Select(id => receipts.Single(r => r.Id == id)).ToList();
    }
}

internal static class AccountingEntryDetails
{
    public static async Task<AccountingEntryDetailDto> Load(AppDbContext context, int clubId, int entryId, CancellationToken cancellationToken)
    {
        var entry = await context.AccountingEntries.AsNoTracking().Include(e => e.Lines).Include(e => e.Changes)
            .SingleRequiredAsync(e => e.Id == entryId && e.ClubId == clubId, cancellationToken);
        var receipts = await context.AccountingReceipts.AsNoTracking()
            .Where(r => r.ClubId == clubId && r.EntryId == entry.Id)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
        var kinds = await context.CategoryKinds(clubId, cancellationToken);
        var names = await context.MemberNames(clubId, entry.Changes.Select(c => c.ByMemberId).Concat(receipts.Select(r => r.UploadedByMemberId)), cancellationToken);

        var dto = entry.ToDto(id => kinds[id], receipts.Count);
        return new AccountingEntryDetailDto
        {
            Id = dto.Id,
            FiscalYearId = dto.FiscalYearId,
            Number = dto.Number,
            Date = dto.Date,
            Text = dto.Text,
            Kind = dto.Kind,
            MoneyAccountId = dto.MoneyAccountId,
            ToMoneyAccountId = dto.ToMoneyAccountId,
            TransferAmountCents = dto.TransferAmountCents,
            AmountCents = dto.AmountCents,
            CounterpartyMemberId = dto.CounterpartyMemberId,
            CounterpartyName = dto.CounterpartyName,
            Status = dto.Status,
            CancelledAt = dto.CancelledAt,
            CancelReason = dto.CancelReason,
            Lines = dto.Lines,
            ReceiptCount = dto.ReceiptCount,
            Receipts = receipts.Select(r => r.ToDto(entry.Number, names)).ToList(),
            History = entry.Changes.OrderBy(c => c.At).ThenBy(c => c.Id).Select(c => ToDto(c, names)).ToList(),
        };
    }

    private static AccountingEntryChangeDto ToDto(AccountingEntryChange change, IReadOnlyDictionary<int, string> names)
    {
        var snapshot = AccountingEntry.EntrySnapshot.Read(change.Snapshot) ?? throw new InvalidOperationException("Unreadable entry snapshot.");
        return new AccountingEntryChangeDto
        {
            Kind = (global::Bookennis.Shared.Controller.Accounting.AccountingEntryChangeKind)change.Kind,
            At = change.At,
            ByName = change.ByMemberId is { } by ? names.GetValueOrDefault(by) : null,
            Date = snapshot.Date,
            Text = snapshot.Text,
            EntryKind = (global::Bookennis.Shared.Controller.Accounting.AccountingEntryKind)snapshot.Kind,
            MoneyAccountId = snapshot.MoneyAccountId,
            ToMoneyAccountId = snapshot.ToMoneyAccountId,
            TransferAmountCents = snapshot.TransferAmountCents,
            CounterpartyName = snapshot.CounterpartyName,
            Lines = snapshot.Lines.Select(l => new AccountingEntryLineDto { CategoryId = l.CategoryId, CostCenterId = l.CostCenterId, AmountCents = l.AmountCents }).ToList(),
            CancelReason = snapshot.CancelReason,
            ReceiptIds = snapshot.ReceiptIds?.ToList() ?? [],
        };
    }
}

/// <summary>
/// Books a new entry: in the fiscal year of its date, with the receipts it lists, and carries the balances into the years after.
/// Used by the treasurer's entries, the suggestions of the automatic entries and the bank lines. The caller holds the accounting lock.
/// </summary>
internal static class AccountingEntryCreation
{
    // allowArchivedLines: archived categories and cost centers are taken (the payment of an invoice written before they were archived)
    public static async Task<AccountingEntry> Create(AppDbContext context, int clubId, int? memberId, SaveAccountingEntryModel model, DateTimeOffset now, CancellationToken cancellationToken,
        bool allowArchivedLines = false)
    {
        var date = model.Date;
        var year = await context.FiscalYears.Include(y => y.OpeningBalances).SingleOrDefaultAsync(y => y.ClubId == clubId && y.StartDate <= date && y.EndDate >= date, cancellationToken)
                   ?? throw new PreconditionException(AccountingErrorCode.AccountingFiscalYearMissing, [date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)], "No fiscal year contains the date.");
        var data = await AccountingEntryInput.Check(context, clubId, model, null, cancellationToken, allowArchivedLines);
        var receipts = await AccountingEntryInput.Receipts(context, clubId, model.ReceiptIds ?? [], null, cancellationToken);

        var entry = new AccountingEntry(year, data, receipts.Select(r => r.Id).ToList(), memberId, now);
        context.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var receipt in receipts)
            receipt.AttachTo(entry);
        await context.SaveChangesAsync(cancellationToken);
        await AccountingBalances.CarryForward(context, year, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    /// <summary>The model of an entry as Clubnetz suggests it (a suggestion, a bank line with a rule).</summary>
    public static SaveAccountingEntryModel ToModel(AccountingEntry.EntryData data) => new()
    {
        Date = data.Date,
        Text = data.Text,
        Kind = (global::Bookennis.Shared.Controller.Accounting.AccountingEntryKind)data.Kind,
        MoneyAccountId = data.MoneyAccountId,
        ToMoneyAccountId = data.ToMoneyAccountId,
        TransferAmountCents = data.TransferAmountCents,
        Lines = data.Lines.Select(l => new SaveAccountingEntryLineModel { CategoryId = l.CategoryId, CostCenterId = l.CostCenterId, AmountCents = l.AmountCents }).ToList(),
        CounterpartyMemberId = data.CounterpartyMemberId,
        CounterpartyName = data.CounterpartyName,
    };
}
