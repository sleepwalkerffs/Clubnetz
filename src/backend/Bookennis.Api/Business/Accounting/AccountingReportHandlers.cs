using System.Globalization;
using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using AccountingEntryKind = Bookennis.Domain.Accounting.AccountingEntryKind;
using AccountingEntryStatus = Bookennis.Domain.Accounting.AccountingEntryStatus;
using AccountingProposalStatus = Bookennis.Domain.Accounting.AccountingProposalStatus;
using BankTransactionStatus = Bookennis.Domain.Accounting.BankTransactionStatus;
using TaxSphere = Bookennis.Domain.Accounting.TaxSphere;

namespace Bookennis.Api.Business.Accounting;

/// <summary>The overview of a fiscal year: opening balance, income, expenses, transfers and closing balance per money account (Vermögensübersicht).</summary>
public record GetAccountingSummary(int ClubId, int FiscalYearId) : IQuery<AccountingSummaryDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingSummary, AccountingSummaryDto>
    {
        public async Task<AccountingSummaryDto> Handle(GetAccountingSummary request, CancellationToken cancellationToken)
        {
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            var balances = await AccountingBalances.Load(context, year, cancellationToken);

            var entries = context.AccountingEntries.Where(e => e.ClubId == request.ClubId && e.FiscalYearId == year.Id);
            var withReceipt = context.AccountingReceipts.Where(r => r.ClubId == request.ClubId && r.EntryId != null).Select(r => r.EntryId!.Value);

            return new AccountingSummaryDto
            {
                FiscalYearId = year.Id,
                Accounts = balances,
                OpeningCents = balances.Sum(b => b.OpeningCents),
                IncomeCents = balances.Sum(b => b.IncomeCents),
                ExpenseCents = balances.Sum(b => b.ExpenseCents),
                ClosingCents = balances.Sum(b => b.ClosingCents),
                EntryCount = await entries.CountAsync(e => e.Status == AccountingEntryStatus.Active, cancellationToken),
                CancelledCount = await entries.CountAsync(e => e.Status == AccountingEntryStatus.Cancelled, cancellationToken),
                EntriesWithoutReceipt = await entries.CountAsync(e => e.Status == AccountingEntryStatus.Active && e.Kind == AccountingEntryKind.Booking && !withReceipt.Contains(e.Id), cancellationToken),
                ReceiptsInInbox = await context.AccountingReceipts.CountAsync(r => r.ClubId == request.ClubId && r.EntryId == null, cancellationToken),
                OpenProposals = await context.AccountingProposals.CountAsync(p => p.ClubId == request.ClubId && p.Status == AccountingProposalStatus.Open
                                                                                  && p.Date >= year.StartDate && p.Date <= year.EndDate, cancellationToken),
                OpenBankLines = await context.BankTransactions.CountAsync(t => t.ClubId == request.ClubId && t.Status == BankTransactionStatus.Open
                                                                               && t.BookingDate >= year.StartDate && t.BookingDate <= year.EndDate, cancellationToken),
            };
        }
    }
}

/// <summary>The income/expense statement (Einnahmen-Ausgaben-Rechnung) of a fiscal year by tax sphere and category, next to the year before.</summary>
public record GetIncomeStatement(int ClubId, int FiscalYearId) : IQuery<IncomeStatementDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetIncomeStatement, IncomeStatementDto>
    {
        public async Task<IncomeStatementDto> Handle(GetIncomeStatement request, CancellationToken cancellationToken)
        {
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            var previous = await context.GetPreviousFiscalYear(year, cancellationToken);
            var categories = await context.AccountingCategories.AsNoTracking().Where(c => c.ClubId == request.ClubId).ToListAsync(cancellationToken);

            var current = AccountingBalances.ByCategory(await context.ActiveEntries(request.ClubId, year.Id, cancellationToken));
            var before = previous is null ? null : AccountingBalances.ByCategory(await context.ActiveEntries(request.ClubId, previous.Id, cancellationToken));

            var spheres = categories.InChartOrder()
                .Where(c => current.ContainsKey(c.Id) || before?.ContainsKey(c.Id) == true)
                .GroupBy(c => c.TaxSphere)
                .OrderBy(g => g.Key)
                .Select(g => Sphere(g.Key, g.ToList(), current, before))
                .ToList();

            return new IncomeStatementDto
            {
                FiscalYearId = year.Id,
                PreviousFiscalYearId = previous?.Id,
                Spheres = spheres,
                IncomeCents = spheres.Sum(s => s.IncomeCents),
                ExpenseCents = spheres.Sum(s => s.ExpenseCents),
                PreviousIncomeCents = before is null ? null : spheres.Sum(s => s.PreviousIncomeCents ?? 0),
                PreviousExpenseCents = before is null ? null : spheres.Sum(s => s.PreviousExpenseCents ?? 0),
            };
        }

        private static IncomeStatementSphereDto Sphere(TaxSphere sphere, List<AccountingCategory> categories, Dictionary<int, long> current, Dictionary<int, long>? before)
        {
            long Sum(Dictionary<int, long> amounts, AccountingCategoryKind kind) => categories.Where(c => c.Kind == kind).Sum(c => amounts.GetValueOrDefault(c.Id));

            return new IncomeStatementSphereDto
            {
                TaxSphere = (global::Bookennis.Shared.Controller.Accounting.TaxSphere)sphere,
                IncomeCents = Sum(current, AccountingCategoryKind.Income),
                ExpenseCents = Sum(current, AccountingCategoryKind.Expense),
                PreviousIncomeCents = before is null ? null : Sum(before, AccountingCategoryKind.Income),
                PreviousExpenseCents = before is null ? null : Sum(before, AccountingCategoryKind.Expense),
                Categories = categories.Select(c => new IncomeStatementCategoryDto
                {
                    CategoryId = c.Id,
                    Kind = (global::Bookennis.Shared.Controller.Accounting.AccountingCategoryKind)c.Kind,
                    AmountCents = current.GetValueOrDefault(c.Id),
                    PreviousAmountCents = before?.GetValueOrDefault(c.Id),
                }).ToList(),
            };
        }
    }
}

/// <summary>Income and expenses of a fiscal year by cost center, with the categories of each.</summary>
public record GetCostCenterReport(int ClubId, int FiscalYearId) : IQuery<CostCenterReportDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetCostCenterReport, CostCenterReportDto>
    {
        public async Task<CostCenterReportDto> Handle(GetCostCenterReport request, CancellationToken cancellationToken)
        {
            var year = await context.FiscalYears.AsNoTracking().SingleRequiredAsync(y => y.Id == request.FiscalYearId && y.ClubId == request.ClubId, cancellationToken);
            var categories = await context.AccountingCategories.AsNoTracking().Where(c => c.ClubId == request.ClubId).ToListAsync(cancellationToken);
            var order = categories.InChartOrder().Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
            var kinds = categories.ToDictionary(c => c.Id, c => c.Kind);
            var costCenters = await context.CostCenters.AsNoTracking().Where(c => c.ClubId == request.ClubId).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
            var lines = (await context.ActiveEntries(request.ClubId, year.Id, cancellationToken)).Where(e => e.Kind == AccountingEntryKind.Booking).SelectMany(e => e.Lines).ToList();

            var rows = lines.GroupBy(l => l.CostCenterId)
                .OrderBy(g => g.Key is null)
                .ThenBy(g => g.Key is { } id ? costCenters.GetValueOrDefault(id, "") : "", StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new CostCenterReportRowDto
                {
                    CostCenterId = g.Key,
                    IncomeCents = g.Where(l => kinds[l.CategoryId] == AccountingCategoryKind.Income).Sum(l => (long)l.AmountCents),
                    ExpenseCents = g.Where(l => kinds[l.CategoryId] == AccountingCategoryKind.Expense).Sum(l => (long)l.AmountCents),
                    Categories = g.GroupBy(l => l.CategoryId)
                        .OrderBy(c => order[c.Key])
                        .Select(c => new IncomeStatementCategoryDto
                        {
                            CategoryId = c.Key,
                            Kind = (global::Bookennis.Shared.Controller.Accounting.AccountingCategoryKind)kinds[c.Key],
                            AmountCents = c.Sum(l => (long)l.AmountCents),
                        })
                        .ToList(),
                })
                .ToList();

            return new CostCenterReportDto { FiscalYearId = year.Id, Rows = rows };
        }
    }
}

/// <summary>The cash book / account sheet of a money account in a fiscal year: every active entry that moved its money, with the running balance.</summary>
public record GetAccountSheet(int ClubId, int FiscalYearId, int MoneyAccountId) : IQuery<AccountSheetDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountSheet, AccountSheetDto>
    {
        public async Task<AccountSheetDto> Handle(GetAccountSheet request, CancellationToken cancellationToken)
        {
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            var account = await context.MoneyAccounts.AsNoTracking().SingleRequiredAsync(a => a.Id == request.MoneyAccountId && a.ClubId == request.ClubId, cancellationToken);
            var kinds = await context.CategoryKinds(request.ClubId, cancellationToken);
            var entries = await context.ActiveEntries(request.ClubId, year.Id, cancellationToken);

            var balance = (long)year.OpeningBalanceCents(account.Id);
            var opening = balance;
            var rows = new List<AccountSheetRowDto>();
            foreach (var entry in entries.Where(e => e.MoneyAccountId == account.Id || e.ToMoneyAccountId == account.Id))
            {
                var amount = entry.EffectOn(account.Id, id => kinds[id]);
                balance += amount;
                rows.Add(new AccountSheetRowDto
                {
                    EntryId = entry.Id,
                    Number = entry.Number,
                    Date = entry.Date,
                    Text = entry.Text,
                    CounterpartyName = entry.CounterpartyName,
                    AmountCents = amount,
                    BalanceCents = balance,
                });
            }

            return new AccountSheetDto { FiscalYearId = year.Id, MoneyAccountId = account.Id, OpeningCents = opening, ClosingCents = balance, Rows = rows };
        }
    }
}

/// <summary>Sums of a fiscal year, shared by the reports and closing the year.</summary>
internal static class AccountingBalances
{
    /// <summary>Every money account of the club (also archived ones) with opening balance, movements and closing balance in the year.</summary>
    public static async Task<List<MoneyAccountBalanceDto>> Load(AppDbContext context, FiscalYear year, CancellationToken cancellationToken)
    {
        var accounts = await context.MoneyAccounts.AsNoTracking().Where(a => a.ClubId == year.ClubId).OrderBy(a => a.SortOrder).ThenBy(a => a.Name).ToListAsync(cancellationToken);
        var kinds = await context.CategoryKinds(year.ClubId, cancellationToken);
        var entries = await context.ActiveEntries(year.ClubId, year.Id, cancellationToken);

        return accounts.Select(account =>
        {
            var own = entries.Where(e => e.Kind == AccountingEntryKind.Booking && e.MoneyAccountId == account.Id).SelectMany(e => e.Lines).ToList();
            var income = own.Where(l => kinds[l.CategoryId] == AccountingCategoryKind.Income).Sum(l => (long)l.AmountCents);
            var expense = own.Where(l => kinds[l.CategoryId] == AccountingCategoryKind.Expense).Sum(l => (long)l.AmountCents);
            var transfersIn = entries.Where(e => e.Kind == AccountingEntryKind.Transfer && e.ToMoneyAccountId == account.Id).Sum(e => (long)(e.TransferAmountCents ?? 0));
            var transfersOut = entries.Where(e => e.Kind == AccountingEntryKind.Transfer && e.MoneyAccountId == account.Id).Sum(e => (long)(e.TransferAmountCents ?? 0));
            var opening = (long)year.OpeningBalanceCents(account.Id);

            return new MoneyAccountBalanceDto
            {
                MoneyAccountId = account.Id,
                OpeningCents = opening,
                IncomeCents = income,
                ExpenseCents = expense,
                TransfersInCents = transfersIn,
                TransfersOutCents = transfersOut,
                ClosingCents = opening + income - expense + transfersIn - transfersOut,
            };
        }).ToList();
    }

    /// <summary>
    /// After a change in <paramref name="year"/> (saved already): the open years after it start with the closing balances of the year
    /// before them, for every money account that year used. Also while a closed year is open again for a correction. The caller saves.
    /// </summary>
    public static async Task CarryForward(AppDbContext context, FiscalYear year, CancellationToken cancellationToken)
    {
        // With its opening balances, also when the caller loaded the year without them
        var current = await context.GetFiscalYear(year.ClubId, year.Id, cancellationToken);
        while (await context.GetNextFiscalYear(current, cancellationToken) is { IsOpen: true } next)
        {
            var balances = await Load(context, current, cancellationToken);
            next.CarryOver(balances
                .Where(b => b.OpeningCents != 0 || b.IncomeCents != 0 || b.ExpenseCents != 0 || b.TransfersInCents != 0 || b.TransfersOutCents != 0)
                .ToDictionary(b => b.MoneyAccountId, b => ToCents(b.ClosingCents)));
            current = next;
        }
    }

    private static int ToCents(long cents) => cents is >= int.MinValue and <= int.MaxValue
        ? (int)cents
        : throw new PreconditionException(AccountingErrorCode.AccountingAmountInvalid, [(AccountingEntry.MaxAmountCents / 100).ToString(CultureInfo.InvariantCulture)], "The balance is too large.");

    /// <summary>The sum of the lines per category (in the direction of the category, refunds count negative).</summary>
    public static Dictionary<int, long> ByCategory(IEnumerable<AccountingEntry> entries)
        => entries.Where(e => e.Kind == AccountingEntryKind.Booking)
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(l => (long)l.AmountCents));
}
