using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using MoneyAccountKind = Bookennis.Domain.Accounting.MoneyAccountKind;
using FiscalYearStatus = Bookennis.Domain.Accounting.FiscalYearStatus;
using TaxSphere = Bookennis.Domain.Accounting.TaxSphere;

namespace Bookennis.Api.Business.Accounting;

// ---------- money accounts ----------

public record CreateMoneyAccount(int ClubId, SaveMoneyAccountModel Model) : ICommand<MoneyAccountDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateMoneyAccount, MoneyAccountDto>
    {
        public async Task<MoneyAccountDto> Handle(CreateMoneyAccount request, CancellationToken cancellationToken)
        {
            await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var account = new MoneyAccount(request.ClubId, request.Model.ToData());
            account.SetArchived(request.Model.IsArchived);
            await context.EnsureUniqueName(context.MoneyAccounts, account, cancellationToken);

            context.Add(account);
            await context.SaveChangesAsync(cancellationToken);
            return account.ToDto(isUsed: false);
        }
    }
}

public record UpdateMoneyAccount(int ClubId, int MoneyAccountId, SaveMoneyAccountModel Model) : ICommand<MoneyAccountDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateMoneyAccount, MoneyAccountDto>
    {
        public async Task<MoneyAccountDto> Handle(UpdateMoneyAccount request, CancellationToken cancellationToken)
        {
            var account = await context.MoneyAccounts.SingleRequiredAsync(a => a.Id == request.MoneyAccountId && a.ClubId == request.ClubId, cancellationToken);
            account.Update(request.Model.ToData());
            account.SetArchived(request.Model.IsArchived);
            await context.EnsureUniqueName(context.MoneyAccounts, account, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return account.ToDto(await context.IsUsed(account, cancellationToken));
        }
    }
}

/// <summary>Deletes a money account nothing uses (no entry, no opening balance other than 0). A used one is archived instead.</summary>
public record DeleteMoneyAccount(int ClubId, int MoneyAccountId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteMoneyAccount>
    {
        public async Task<Unit> Handle(DeleteMoneyAccount request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var account = await context.MoneyAccounts.SingleRequiredAsync(a => a.Id == request.MoneyAccountId && a.ClubId == request.ClubId, cancellationToken);
            if (await context.IsUsed(account, cancellationToken))
                throw new PreconditionException(AccountingErrorCode.AccountingInUse, [account.Name], "The money account is used.");

            var years = await context.FiscalYears.Include(y => y.OpeningBalances)
                .Where(y => y.ClubId == request.ClubId && y.OpeningBalances.Any(b => b.MoneyAccountId == account.Id))
                .ToListAsync(cancellationToken);
            foreach (var balance in years.SelectMany(y => y.OpeningBalances).Where(b => b.MoneyAccountId == account.Id))
                context.Remove(balance);

            context.MoneyAccounts.Remove(account);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

// ---------- categories ----------

public record CreateAccountingCategory(int ClubId, SaveAccountingCategoryModel Model) : ICommand<AccountingCategoryDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateAccountingCategory, AccountingCategoryDto>
    {
        public async Task<AccountingCategoryDto> Handle(CreateAccountingCategory request, CancellationToken cancellationToken)
        {
            await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var category = new AccountingCategory(request.ClubId, request.Model.ToData());
            category.SetArchived(request.Model.IsArchived);
            await context.EnsureUniqueName(category, cancellationToken);

            context.Add(category);
            await context.SaveChangesAsync(cancellationToken);
            return category.ToDto(isUsed: false);
        }
    }
}

public record UpdateAccountingCategory(int ClubId, int CategoryId, SaveAccountingCategoryModel Model) : ICommand<AccountingCategoryDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateAccountingCategory, AccountingCategoryDto>
    {
        public async Task<AccountingCategoryDto> Handle(UpdateAccountingCategory request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var category = await context.AccountingCategories.SingleRequiredAsync(c => c.Id == request.CategoryId && c.ClubId == request.ClubId, cancellationToken);
            var isUsed = await context.IsUsed(category, cancellationToken);
            var isUsedInClosedYear = isUsed && await context.AccountingEntries.AnyAsync(e => e.ClubId == request.ClubId
                && e.Lines.Any(l => l.CategoryId == category.Id)
                && context.FiscalYears.Any(y => y.Id == e.FiscalYearId && y.Status == FiscalYearStatus.Closed), cancellationToken);
            category.Update(request.Model.ToData(), isUsed, isUsedInClosedYear);
            category.SetArchived(request.Model.IsArchived);
            await context.EnsureUniqueName(category, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return category.ToDto(isUsed);
        }
    }
}

public record DeleteAccountingCategory(int ClubId, int CategoryId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteAccountingCategory>
    {
        public async Task<Unit> Handle(DeleteAccountingCategory request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var category = await context.AccountingCategories.SingleRequiredAsync(c => c.Id == request.CategoryId && c.ClubId == request.ClubId, cancellationToken);
            if (await context.IsUsed(category, cancellationToken))
                throw new PreconditionException(AccountingErrorCode.AccountingInUse, [category.Name], "The category is used.");

            context.AccountingCategories.Remove(category);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

// ---------- cost centers ----------

public record CreateCostCenter(int ClubId, SaveCostCenterModel Model) : ICommand<CostCenterDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateCostCenter, CostCenterDto>
    {
        public async Task<CostCenterDto> Handle(CreateCostCenter request, CancellationToken cancellationToken)
        {
            await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var costCenter = new CostCenter(request.ClubId, request.Model.Name);
            costCenter.SetArchived(request.Model.IsArchived);
            await context.EnsureUniqueName(context.CostCenters, costCenter, cancellationToken);

            context.Add(costCenter);
            await context.SaveChangesAsync(cancellationToken);
            return costCenter.ToDto(isUsed: false);
        }
    }
}

public record UpdateCostCenter(int ClubId, int CostCenterId, SaveCostCenterModel Model) : ICommand<CostCenterDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateCostCenter, CostCenterDto>
    {
        public async Task<CostCenterDto> Handle(UpdateCostCenter request, CancellationToken cancellationToken)
        {
            var costCenter = await context.CostCenters.SingleRequiredAsync(c => c.Id == request.CostCenterId && c.ClubId == request.ClubId, cancellationToken);
            costCenter.Rename(request.Model.Name);
            costCenter.SetArchived(request.Model.IsArchived);
            await context.EnsureUniqueName(context.CostCenters, costCenter, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return costCenter.ToDto(await context.IsUsed(costCenter, cancellationToken));
        }
    }
}

public record DeleteCostCenter(int ClubId, int CostCenterId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteCostCenter>
    {
        public async Task<Unit> Handle(DeleteCostCenter request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var costCenter = await context.CostCenters.SingleRequiredAsync(c => c.Id == request.CostCenterId && c.ClubId == request.ClubId, cancellationToken);
            if (await context.IsUsed(costCenter, cancellationToken))
                throw new PreconditionException(AccountingErrorCode.AccountingInUse, [costCenter.Name], "The cost center is used.");

            context.CostCenters.Remove(costCenter);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

internal static class AccountingMasterDataExtensions
{
    public static MoneyAccount.AccountData ToData(this SaveMoneyAccountModel model) => new(model.Name, (MoneyAccountKind)model.Kind, model.Iban, model.SortOrder);

    public static AccountingCategory.CategoryData ToData(this SaveAccountingCategoryModel model)
        => new((AccountingCategoryKind)model.Kind, model.Number, model.Name, (TaxSphere)model.TaxSphere);

    public static MoneyAccountDto ToDto(this MoneyAccount account, bool isUsed) => new()
    {
        Id = account.Id,
        Name = account.Name,
        Kind = (global::Bookennis.Shared.Controller.Accounting.MoneyAccountKind)account.Kind,
        Iban = account.Iban,
        IsArchived = account.IsArchived,
        SortOrder = account.SortOrder,
        IsUsed = isUsed,
    };

    public static AccountingCategoryDto ToDto(this AccountingCategory category, bool isUsed) => new()
    {
        Id = category.Id,
        Kind = (global::Bookennis.Shared.Controller.Accounting.AccountingCategoryKind)category.Kind,
        Number = category.Number,
        Name = category.Name,
        TaxSphere = (global::Bookennis.Shared.Controller.Accounting.TaxSphere)category.TaxSphere,
        IsArchived = category.IsArchived,
        IsUsed = isUsed,
    };

    public static CostCenterDto ToDto(this CostCenter costCenter, bool isUsed) => new()
    {
        Id = costCenter.Id,
        Name = costCenter.Name,
        IsArchived = costCenter.IsArchived,
        IsUsed = isUsed,
    };

    /// <summary>Income before expenses, then by number (categories without one last) and name.</summary>
    public static IEnumerable<AccountingCategory> InChartOrder(this IEnumerable<AccountingCategory> categories)
        => categories.OrderBy(c => c.Kind).ThenBy(c => c.Number == null).ThenBy(c => c.Number, StringComparer.Ordinal).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);

    public static async Task<bool> IsUsed(this AppDbContext context, MoneyAccount account, CancellationToken cancellationToken)
        => await context.AccountingEntries.AnyAsync(e => e.ClubId == account.ClubId && (e.MoneyAccountId == account.Id || e.ToMoneyAccountId == account.Id), cancellationToken)
           || await context.FiscalYears.Where(y => y.ClubId == account.ClubId).SelectMany(y => y.OpeningBalances).AnyAsync(b => b.MoneyAccountId == account.Id && b.AmountCents != 0, cancellationToken);

    public static Task<bool> IsUsed(this AppDbContext context, AccountingCategory category, CancellationToken cancellationToken)
        => context.AccountingEntryLines.AnyAsync(l => l.CategoryId == category.Id, cancellationToken);

    public static Task<bool> IsUsed(this AppDbContext context, CostCenter costCenter, CancellationToken cancellationToken)
        => context.AccountingEntryLines.AnyAsync(l => l.CostCenterId == costCenter.Id, cancellationToken);

    public static async Task EnsureUniqueName<T>(this AppDbContext context, IQueryable<T> set, T item, CancellationToken cancellationToken)
        where T : AccountingMasterData
    {
        var pattern = LikePattern(item.Name);
        if (await set.AnyAsync(x => x.ClubId == item.ClubId && x.Id != item.Id && EF.Functions.ILike(x.Name, pattern), cancellationToken))
            throw new PreconditionException(AccountingErrorCode.AccountingNameTaken, [item.Name], "The name is taken.");
    }

    /// <summary>Category names are unique per kind: "Kantine" can be an income and an expense category.</summary>
    public static async Task EnsureUniqueName(this AppDbContext context, AccountingCategory category, CancellationToken cancellationToken)
    {
        var pattern = LikePattern(category.Name);
        if (await context.AccountingCategories.AnyAsync(c => c.ClubId == category.ClubId && c.Id != category.Id && c.Kind == category.Kind && EF.Functions.ILike(c.Name, pattern), cancellationToken))
            throw new PreconditionException(AccountingErrorCode.AccountingNameTaken, [category.Name], "The name is taken.");
    }

    /// <summary>The text as an ILIKE pattern that matches only itself (any case).</summary>
    public static string LikePattern(string text) => text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
