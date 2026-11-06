using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Accounting;
using Microsoft.EntityFrameworkCore;
using FiscalYearAuditStatus = Bookennis.Domain.Accounting.FiscalYearAuditStatus;

namespace Bookennis.Api.Business.Accounting;

/// <summary>Whether the accounting is set up, its fiscal years and everything entries are made with. What the caller may do is decided by the controller.</summary>
public record GetAccounting(int ClubId, bool CanManage, bool CanAudit) : IQuery<AccountingDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccounting, AccountingDto>
    {
        public async Task<AccountingDto> Handle(GetAccounting request, CancellationToken cancellationToken)
        {
            var settings = await context.AccountingSettings.AsNoTracking().SingleOrDefaultAsync(s => s.ClubId == request.ClubId, cancellationToken);
            if (settings is null)
                return new AccountingDto { IsSetUp = false, CanManage = request.CanManage, CanAudit = request.CanAudit };

            var years = await context.FiscalYears.AsNoTracking().Include(y => y.OpeningBalances)
                .Where(y => y.ClubId == request.ClubId)
                .OrderByDescending(y => y.StartDate)
                .ToListAsync(cancellationToken);
            var names = await context.MemberNames(request.ClubId, years.SelectMany(y => new[] { y.ClosedByMemberId, y.AuditedByMemberId }), cancellationToken);

            var entries = context.AccountingEntries.Where(e => e.ClubId == request.ClubId);
            var usedAccounts = await entries.Select(e => e.MoneyAccountId)
                .Union(entries.Where(e => e.ToMoneyAccountId != null).Select(e => e.ToMoneyAccountId!.Value))
                .Union(context.FiscalYears.Where(y => y.ClubId == request.ClubId).SelectMany(y => y.OpeningBalances).Where(b => b.AmountCents != 0).Select(b => b.MoneyAccountId))
                .ToListAsync(cancellationToken);
            var lines = entries.SelectMany(e => e.Lines);
            var usedCategories = await lines.Select(l => l.CategoryId).Distinct().ToListAsync(cancellationToken);
            var usedCostCenters = await lines.Where(l => l.CostCenterId != null).Select(l => l.CostCenterId!.Value).Distinct().ToListAsync(cancellationToken);

            var accounts = await context.MoneyAccounts.AsNoTracking().Where(a => a.ClubId == request.ClubId).OrderBy(a => a.SortOrder).ThenBy(a => a.Name).ToListAsync(cancellationToken);
            var categories = await context.AccountingCategories.AsNoTracking().Where(c => c.ClubId == request.ClubId).ToListAsync(cancellationToken);
            var costCenters = await context.CostCenters.AsNoTracking().Where(c => c.ClubId == request.ClubId).OrderBy(c => c.Name).ToListAsync(cancellationToken);

            return new AccountingDto
            {
                IsSetUp = true,
                FiscalYearStartMonth = settings.FiscalYearStartMonth,
                FiscalYears = years.Select(y => y.ToDto(names)).ToList(),
                MoneyAccounts = accounts.Select(a => a.ToDto(usedAccounts.Contains(a.Id))).ToList(),
                Categories = categories.InChartOrder().Select(c => c.ToDto(usedCategories.Contains(c.Id))).ToList(),
                CostCenters = costCenters.Select(c => c.ToDto(usedCostCenters.Contains(c.Id))).ToList(),
                CanManage = request.CanManage,
                CanAudit = request.CanAudit,
            };
        }
    }
}

/// <summary>
/// Switches the accounting on: the start month of the fiscal year, the first fiscal year, a bank account and a cash box and the
/// categories of the Austrian template, in the language of the treasurer.
/// </summary>
public record SetUpAccounting(int ClubId, SetUpAccountingModel Model, DateTimeOffset Now) : ICommand
{
    public class Handler(AppDbContext context, IUserLanguageAccessor languageAccessor) : IRequestHandler<SetUpAccounting>
    {
        public async Task<Unit> Handle(SetUpAccounting request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            if (await context.AccountingSettings.AnyAsync(s => s.ClubId == request.ClubId, cancellationToken))
                throw new PreconditionException(AccountingErrorCode.AccountingAlreadySetUp, "The accounting is already set up.");

            var settings = new AccountingSettings(request.ClubId, request.Model.FiscalYearStartMonth);
            var year = FiscalYear.First(request.ClubId, settings.FiscalYearStartMonth, request.Model.FirstYear, AccountingQueries.Today(request.Now));
            var german = languageAccessor.GetCultureInfo().TwoLetterISOLanguageName == "de";

            context.Add(settings);
            context.Add(year);
            context.AddRange(AccountingTemplate.CreateMoneyAccounts(request.ClubId, german));
            context.AddRange(AccountingTemplate.CreateCategories(request.ClubId, german));
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>Creates the fiscal year after the newest one (as soon as it starts within a year).</summary>
public record CreateFiscalYear(int ClubId, DateTimeOffset Now) : ICommand<FiscalYearDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateFiscalYear, FiscalYearDto>
    {
        public async Task<FiscalYearDto> Handle(CreateFiscalYear request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            await context.GetAccountingSettings(request.ClubId, cancellationToken);

            var newest = await context.FiscalYears.Where(y => y.ClubId == request.ClubId).OrderByDescending(y => y.StartDate).FirstAsync(cancellationToken);
            var next = newest.CreateNext(AccountingQueries.Today(request.Now));
            context.Add(next);
            await context.SaveChangesAsync(cancellationToken);
            return next.ToDto(new Dictionary<int, string>());
        }
    }
}

/// <summary>The opening balances of the first fiscal year (later years get the closing balances of the year before).</summary>
public record SetOpeningBalances(int ClubId, int FiscalYearId, SetOpeningBalancesModel Model) : ICommand<FiscalYearDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<SetOpeningBalances, FiscalYearDto>
    {
        public async Task<FiscalYearDto> Handle(SetOpeningBalances request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);

            var accountIds = request.Model.Balances.Select(b => b.MoneyAccountId).Distinct().ToList();
            var known = await context.MoneyAccounts.Where(a => a.ClubId == request.ClubId && accountIds.Contains(a.Id)).Select(a => a.Id).ToListAsync(cancellationToken);
            if (known.Count != accountIds.Count)
                throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(MoneyAccount));

            foreach (var balance in request.Model.Balances)
                year.SetOpeningBalance(balance.MoneyAccountId, balance.AmountCents);

            await context.SaveChangesAsync(cancellationToken);
            return await YearDto(context, year, cancellationToken);
        }
    }

    internal static async Task<FiscalYearDto> YearDto(AppDbContext context, FiscalYear year, CancellationToken cancellationToken)
        => year.ToDto(await context.MemberNames(year.ClubId, [year.ClosedByMemberId, year.AuditedByMemberId], cancellationToken));
}

/// <summary>
/// Closes a fiscal year: its entries are locked and the closing balances become the opening balances of the next year (which is
/// created when the club does not have it yet). The year before must be closed first.
/// </summary>
public record CloseFiscalYear(int ClubId, int UserId, int FiscalYearId, DateTimeOffset Now) : ICommand<FiscalYearDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CloseFiscalYear, FiscalYearDto>
    {
        public async Task<FiscalYearDto> Handle(CloseFiscalYear request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            var previous = await context.GetPreviousFiscalYear(year, cancellationToken);
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            year.Close(previous, memberId, request.Now);

            var balances = await AccountingBalances.Load(context, year, cancellationToken);
            var next = await context.GetNextFiscalYear(year, cancellationToken);
            if (next is null)
            {
                next = year.CreateNext(AccountingQueries.Today(request.Now));
                context.Add(next);
            }

            next.CarryOver(balances.ToDictionary(b => b.MoneyAccountId, b => checked((int)b.ClosingCents)));
            await context.SaveChangesAsync(cancellationToken);
            return await SetOpeningBalances.YearDto(context, year, cancellationToken);
        }
    }
}

/// <summary>Opens a closed fiscal year again (the year after it must be open). The audit of the year no longer counts.</summary>
public record ReopenFiscalYear(int ClubId, int FiscalYearId) : ICommand<FiscalYearDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<ReopenFiscalYear, FiscalYearDto>
    {
        public async Task<FiscalYearDto> Handle(ReopenFiscalYear request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            year.Reopen(await context.GetNextFiscalYear(year, cancellationToken));
            await context.SaveChangesAsync(cancellationToken);
            return await SetOpeningBalances.YearDto(context, year, cancellationToken);
        }
    }
}

/// <summary>
/// The auditor (Rechnungsprüfer) records the audit of a closed fiscal year. Only a member with the role auditor who is not also a
/// treasurer of the club: nobody audits their own accounts, and in support mode there is no member to sign it.
/// </summary>
public record RecordFiscalYearAudit(int ClubId, int UserId, int FiscalYearId, RecordAuditModel Model, DateTimeOffset Now) : ICommand<FiscalYearDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<RecordFiscalYearAudit, FiscalYearDto>
    {
        public async Task<FiscalYearDto> Handle(RecordFiscalYearAudit request, CancellationToken cancellationToken)
        {
            var auditor = await context.ClubMembers.AsNoTracking()
                .Where(m => m.UserId == request.UserId && m.ClubId == request.ClubId)
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (auditor is null || !auditor.UserRoles.Contains(MemberRole.Auditor))
                throw new PreconditionException(AccountingErrorCode.AccountingAuditorRequired, "Only an auditor of the club records an audit.");
            if (auditor.UserRoles.Contains(MemberRole.Treasurer))
                throw new PreconditionException(AccountingErrorCode.AccountingAuditorIsTreasurer, "A treasurer doesn't audit their own accounts.");

            await context.LockAccounting(request.ClubId, cancellationToken);
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            year.RecordAudit((FiscalYearAuditStatus)request.Model.Status, request.Model.Comment, auditor.Id, request.Now);
            await context.SaveChangesAsync(cancellationToken);
            return await SetOpeningBalances.YearDto(context, year, cancellationToken);
        }
    }
}
