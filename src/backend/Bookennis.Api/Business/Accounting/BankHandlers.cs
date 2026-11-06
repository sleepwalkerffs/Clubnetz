using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using AccountingEntryKind = Bookennis.Domain.Accounting.AccountingEntryKind;
using AccountingProposalStatus = Bookennis.Domain.Accounting.AccountingProposalStatus;
using BankRuleDirection = Bookennis.Domain.Accounting.BankRuleDirection;
using BankStatementFormat = Bookennis.Domain.Accounting.BankStatementFormat;
using BankTransactionStatus = Bookennis.Domain.Accounting.BankTransactionStatus;

namespace Bookennis.Api.Business.Accounting;

/// <summary>
/// Imports a bank statement file (camt.053, MT940 or CSV) into a money account. Lines imported before are left out. A CSV whose columns
/// are not recognized is not imported: the result carries its first rows, and the upload is repeated with <paramref name="Layout"/>,
/// which is then kept for the money account.
/// </summary>
public record ImportBankStatement(int ClubId, int UserId, int MoneyAccountId, string? FileName, byte[] Data, CsvColumnsDto? Layout, DateTimeOffset Now) : ICommand<BankImportResultDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<ImportBankStatement, BankImportResultDto>
    {
        public async Task<BankImportResultDto> Handle(ImportBankStatement request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var account = await context.MoneyAccounts.SingleRequiredAsync(a => a.Id == request.MoneyAccountId && a.ClubId == request.ClubId, cancellationToken);
            account.EnsureUsable();

            var saved = await context.BankCsvLayouts.SingleOrDefaultAsync(l => l.ClubId == request.ClubId && l.MoneyAccountId == account.Id, cancellationToken);
            var given = request.Layout is null ? null : BankMapper.ToDomain(request.Layout);
            var result = BankStatementReader.Read(request.Data, given ?? saved?.Columns, preferSavedLayout: given is not null);

            if (result.NeedsCsvLayout)
            {
                if (given is not null)
                    throw new PreconditionException(AccountingErrorCode.AccountingBankCsvLayoutInvalid, "No line could be read with these columns.");
                return new BankImportResultDto
                {
                    Format = (global::Bookennis.Shared.Controller.Accounting.BankStatementFormat)result.Format,
                    NeedsCsvLayout = true,
                    Preview = result.Preview!.Select(r => r.ToList()).ToList(),
                    Columns = result.DetectedColumns is null ? null : BankMapper.ToDto(result.DetectedColumns),
                };
            }

            var statement = result.Statement!;
            if (statement.Iban is { } fileIban && account.Iban is { } accountIban && !string.Equals(fileIban.Replace(" ", "", StringComparison.Ordinal), accountIban, StringComparison.OrdinalIgnoreCase))
                throw new PreconditionException(AccountingErrorCode.AccountingBankFileOtherAccount, [fileIban], "The statement is about another account.");

            // A layout the treasurer mapped is kept for the next file of this account
            if (given is not null)
            {
                if (saved is null)
                    context.Add(new BankCsvLayout(request.ClubId, account.Id, given));
                else
                    saved.Change(given);
            }

            // Two lines that look exactly the same on one day are two payments: they are counted, so a later file with both adds neither
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            var lines = new List<(BankStatementLine Line, string Fingerprint)>();
            foreach (var line in statement.Lines.Where(l => Math.Abs(l.AmountCents) <= AccountingEntry.MaxAmountCents))
            {
                var key = BankTransaction.FingerprintOf(line, 0);
                var occurrence = occurrences.GetValueOrDefault(key);
                occurrences[key] = occurrence + 1;
                lines.Add((line, BankTransaction.FingerprintOf(line, occurrence)));
            }

            var fingerprints = lines.Select(l => l.Fingerprint).ToList();
            var known = (await context.BankTransactions.Where(t => t.MoneyAccountId == account.Id && fingerprints.Contains(t.Fingerprint)).Select(t => t.Fingerprint).ToListAsync(cancellationToken)).ToHashSet();
            var newLines = lines.Where(l => !known.Contains(l.Fingerprint)).ToList();

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var import = new BankStatementImport(request.ClubId, account.Id, request.FileName, result.Format, statement, newLines.Count, lines.Count - newLines.Count, memberId, request.Now);
            context.Add(import);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var (line, fingerprint) in newLines)
                context.Add(new BankTransaction(request.ClubId, account.Id, import.Id, line, fingerprint));
            await context.SaveChangesAsync(cancellationToken);

            return new BankImportResultDto
            {
                Format = (global::Bookennis.Shared.Controller.Accounting.BankStatementFormat)result.Format,
                NeedsCsvLayout = false,
                ImportId = import.Id,
                Imported = import.ImportedCount,
                Skipped = import.SkippedCount,
                Columns = result.Format == BankStatementFormat.Csv && result.DetectedColumns is not null ? BankMapper.ToDto(result.DetectedColumns) : null,
            };
        }
    }
}

/// <summary>The imports of a money account (or all), newest first, with the balance check: what the bank says against the books.</summary>
public record GetBankStatementImports(int ClubId, int? MoneyAccountId) : IQuery<List<BankStatementImportDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBankStatementImports, List<BankStatementImportDto>>
    {
        public async Task<List<BankStatementImportDto>> Handle(GetBankStatementImports request, CancellationToken cancellationToken)
        {
            await BankMapper.EnsureAccountOfClub(context, request.ClubId, request.MoneyAccountId, cancellationToken);
            var imports = await context.BankStatementImports.AsNoTracking()
                .Where(i => i.ClubId == request.ClubId && (request.MoneyAccountId == null || i.MoneyAccountId == request.MoneyAccountId))
                .OrderByDescending(i => i.ImportedAt)
                .Take(50)
                .ToListAsync(cancellationToken);
            var names = await context.MemberNames(request.ClubId, imports.Select(i => i.ImportedByMemberId), cancellationToken);

            var result = new List<BankStatementImportDto>();
            foreach (var import in imports)
            {
                long? books = import.ClosingBalanceCents is not null && import.ClosingDate is { } date
                    ? await BankMapper.BalanceAt(context, request.ClubId, import.MoneyAccountId, date, cancellationToken)
                    : null;
                result.Add(new BankStatementImportDto
                {
                    Id = import.Id,
                    MoneyAccountId = import.MoneyAccountId,
                    FileName = import.FileName,
                    Format = (global::Bookennis.Shared.Controller.Accounting.BankStatementFormat)import.Format,
                    FromDate = import.FromDate,
                    ToDate = import.ToDate,
                    ImportedAt = import.ImportedAt,
                    ImportedByName = import.ImportedByMemberId is { } by ? names.GetValueOrDefault(by) : null,
                    ImportedCount = import.ImportedCount,
                    SkippedCount = import.SkippedCount,
                    ClosingBalanceCents = import.ClosingBalanceCents,
                    ClosingDate = import.ClosingDate,
                    BooksBalanceCents = books,
                });
            }

            return result;
        }
    }
}

/// <summary>
/// The bank lines of a status (open by default), newest first. Open lines carry what fits them: entries and suggestions with the same
/// amount on the money account within a week, and the first rule that matches.
/// </summary>
public record GetBankTransactions(int ClubId, int? MoneyAccountId, BankTransactionStatus Status) : IQuery<GetBankTransactionsResult>
{
    public const int MatchDays = 7;

    public class Handler(AppDbContext context) : IRequestHandler<GetBankTransactions, GetBankTransactionsResult>
    {
        public async Task<GetBankTransactionsResult> Handle(GetBankTransactions request, CancellationToken cancellationToken)
        {
            await BankMapper.EnsureAccountOfClub(context, request.ClubId, request.MoneyAccountId, cancellationToken);
            var lines = await context.BankTransactions.AsNoTracking()
                .Where(t => t.ClubId == request.ClubId && t.Status == request.Status && (request.MoneyAccountId == null || t.MoneyAccountId == request.MoneyAccountId))
                .OrderByDescending(t => t.BookingDate).ThenByDescending(t => t.Id)
                .Take(500)
                .ToListAsync(cancellationToken);
            var openCount = await context.BankTransactions.CountAsync(t => t.ClubId == request.ClubId && t.Status == BankTransactionStatus.Open
                                                                             && (request.MoneyAccountId == null || t.MoneyAccountId == request.MoneyAccountId), cancellationToken);

            var entryIds = lines.Where(l => l.EntryId is not null).Select(l => l.EntryId!.Value).ToList();
            var numbers = await context.AccountingEntries.Where(e => entryIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Number, cancellationToken);
            var finder = request.Status == BankTransactionStatus.Open ? await BankMatchFinder.Load(context, request.ClubId, lines, cancellationToken) : null;

            return new GetBankTransactionsResult
            {
                OpenCount = openCount,
                Transactions = lines.Select(line => new BankTransactionDto
                {
                    Id = line.Id,
                    MoneyAccountId = line.MoneyAccountId,
                    BookingDate = line.BookingDate,
                    ValueDate = line.ValueDate,
                    AmountCents = line.AmountCents,
                    CounterpartyName = line.CounterpartyName,
                    CounterpartyIban = line.CounterpartyIban,
                    Reference = line.Reference,
                    Status = (global::Bookennis.Shared.Controller.Accounting.BankTransactionStatus)line.Status,
                    EntryId = line.EntryId,
                    EntryNumber = line.EntryId is { } entryId ? numbers.GetValueOrDefault(entryId) : null,
                    Matches = finder?.Matches(line) ?? [],
                    RuleId = finder?.Rule(line)?.Id,
                }).ToList(),
            };
        }
    }
}

/// <summary>Matches a bank line with an entry that books exactly its amount, or books a suggestion and matches it.</summary>
public record MatchBankTransaction(int ClubId, int UserId, int BankTransactionId, MatchBankTransactionModel Model, DateTimeOffset Now) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<MatchBankTransaction>
    {
        public async Task<Unit> Handle(MatchBankTransaction request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var line = await context.BankTransactions.SingleRequiredAsync(t => t.Id == request.BankTransactionId && t.ClubId == request.ClubId, cancellationToken);
            line.EnsureOpen();

            AccountingEntry entry;
            if (request.Model.ProposalId is { } proposalId)
            {
                var proposal = await context.AccountingProposals.SingleRequiredAsync(p => p.Id == proposalId && p.ClubId == request.ClubId, cancellationToken);
                proposal.EnsureOpen();
                var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
                entry = await AccountingProposalBooking.Book(context, proposal, null, memberId, request.Now, cancellationToken);
            }
            else
            {
                var entryId = request.Model.EntryId ?? throw new EntityNotFoundException(typeof(AccountingEntry));
                entry = await context.AccountingEntries.Include(e => e.Lines).SingleRequiredAsync(e => e.Id == entryId && e.ClubId == request.ClubId, cancellationToken);
            }

            await BankMapper.Match(context, line, entry, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>Books a bank line as a new entry (as the treasurer filled it in) and matches them: the entry must book the amount of the line.</summary>
public record BookBankTransaction(int ClubId, int UserId, int BankTransactionId, SaveAccountingEntryModel Model, DateTimeOffset Now) : ICommand<AccountingEntryDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<BookBankTransaction, AccountingEntryDetailDto>
    {
        public async Task<AccountingEntryDetailDto> Handle(BookBankTransaction request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var line = await context.BankTransactions.SingleRequiredAsync(t => t.Id == request.BankTransactionId && t.ClubId == request.ClubId, cancellationToken);
            line.EnsureOpen();

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var entry = await AccountingEntryCreation.Create(context, request.ClubId, memberId, request.Model, request.Now, cancellationToken);
            await BankMapper.Match(context, line, entry, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return await AccountingEntryDetails.Load(context, request.ClubId, entry.Id, cancellationToken);
        }
    }
}

/// <summary>
/// Books every open line (of a money account, or all) that a rule fits and nothing else does (no entry or suggestion with its amount):
/// with the category, cost center and text of the rule. Lines that can't be booked (e.g. no open fiscal year) stay open.
/// </summary>
public record BookBankTransactionsByRules(int ClubId, int UserId, int? MoneyAccountId, DateTimeOffset Now) : ICommand<BookBankTransactionsByRulesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<BookBankTransactionsByRules, BookBankTransactionsByRulesResult>
    {
        public async Task<BookBankTransactionsByRulesResult> Handle(BookBankTransactionsByRules request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            await BankMapper.EnsureAccountOfClub(context, request.ClubId, request.MoneyAccountId, cancellationToken);
            var lines = await context.BankTransactions
                .Where(t => t.ClubId == request.ClubId && t.Status == BankTransactionStatus.Open && (request.MoneyAccountId == null || t.MoneyAccountId == request.MoneyAccountId))
                .OrderBy(t => t.BookingDate).ThenBy(t => t.Id)
                .ToListAsync(cancellationToken);
            var finder = await BankMatchFinder.Load(context, request.ClubId, lines, cancellationToken);
            var kinds = await context.CategoryKinds(request.ClubId, cancellationToken);
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            // What would stop an entry is checked first, so one line that can't be booked doesn't stop the others
            var years = await context.FiscalYears.AsNoTracking().Where(y => y.ClubId == request.ClubId).ToListAsync(cancellationToken);
            var archivedCategories = await context.AccountingCategories.Where(c => c.ClubId == request.ClubId && c.IsArchived).Select(c => c.Id).ToListAsync(cancellationToken);
            var archivedCostCenters = await context.CostCenters.Where(c => c.ClubId == request.ClubId && c.IsArchived).Select(c => c.Id).ToListAsync(cancellationToken);
            var archivedAccounts = await context.MoneyAccounts.Where(a => a.ClubId == request.ClubId && a.IsArchived).Select(a => a.Id).ToListAsync(cancellationToken);

            var booked = 0;
            foreach (var line in lines)
            {
                if (finder.Rule(line) is not { } rule || finder.Matches(line).Count > 0)
                    continue;
                if (!years.Any(y => y.IsOpen && y.Contains(line.BookingDate)) || archivedCategories.Contains(rule.CategoryId)
                    || rule.CostCenterId is { } costCenterId && archivedCostCenters.Contains(costCenterId) || archivedAccounts.Contains(line.MoneyAccountId))
                    continue;

                var entry = await AccountingEntryCreation.Create(context, request.ClubId, memberId, BankMapper.EntryFor(line, rule, kinds[rule.CategoryId]), request.Now, cancellationToken);
                line.Match(entry, id => kinds[id]);
                await context.SaveChangesAsync(cancellationToken);
                booked++;
            }

            return new BookBankTransactionsByRulesResult { Booked = booked };
        }
    }
}

public record IgnoreBankTransaction(int ClubId, int BankTransactionId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<IgnoreBankTransaction>
    {
        public async Task<Unit> Handle(IgnoreBankTransaction request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var line = await context.BankTransactions.SingleRequiredAsync(t => t.Id == request.BankTransactionId && t.ClubId == request.ClubId, cancellationToken);
            line.Ignore();
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>Puts a matched or ignored line back on the list. A matched entry stays as it is.</summary>
public record ReopenBankTransaction(int ClubId, int BankTransactionId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<ReopenBankTransaction>
    {
        public async Task<Unit> Handle(ReopenBankTransaction request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var line = await context.BankTransactions.SingleRequiredAsync(t => t.Id == request.BankTransactionId && t.ClubId == request.ClubId, cancellationToken);
            line.Reopen();
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

public record GetBankRules(int ClubId) : IQuery<List<BankRuleDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBankRules, List<BankRuleDto>>
    {
        public async Task<List<BankRuleDto>> Handle(GetBankRules request, CancellationToken cancellationToken)
            => (await context.BankRules.AsNoTracking().Where(r => r.ClubId == request.ClubId).OrderBy(r => r.Id).ToListAsync(cancellationToken))
                .Select(BankMapper.ToDto).ToList();
    }
}

public record CreateBankRule(int ClubId, SaveBankRuleModel Model) : ICommand<BankRuleDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateBankRule, BankRuleDto>
    {
        public async Task<BankRuleDto> Handle(CreateBankRule request, CancellationToken cancellationToken)
        {
            await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var rule = new BankRule(request.ClubId, await BankMapper.RuleData(context, request.ClubId, request.Model, cancellationToken));
            context.Add(rule);
            await context.SaveChangesAsync(cancellationToken);
            return BankMapper.ToDto(rule);
        }
    }
}

public record UpdateBankRule(int ClubId, int BankRuleId, SaveBankRuleModel Model) : ICommand<BankRuleDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateBankRule, BankRuleDto>
    {
        public async Task<BankRuleDto> Handle(UpdateBankRule request, CancellationToken cancellationToken)
        {
            var rule = await context.BankRules.SingleRequiredAsync(r => r.Id == request.BankRuleId && r.ClubId == request.ClubId, cancellationToken);
            rule.Change(await BankMapper.RuleData(context, request.ClubId, request.Model, cancellationToken));
            await context.SaveChangesAsync(cancellationToken);
            return BankMapper.ToDto(rule);
        }
    }
}

public record DeleteBankRule(int ClubId, int BankRuleId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteBankRule>
    {
        public async Task<Unit> Handle(DeleteBankRule request, CancellationToken cancellationToken)
        {
            var rule = await context.BankRules.SingleRequiredAsync(r => r.Id == request.BankRuleId && r.ClubId == request.ClubId, cancellationToken);
            context.Remove(rule);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>What fits open bank lines: entries and suggestions with their amount on their money account within a week, and rules.</summary>
internal sealed class BankMatchFinder
{
    private readonly List<(AccountingEntry Entry, Dictionary<int, long> Effects)> entries;
    private readonly List<AccountingProposal> proposals;
    private readonly IReadOnlyDictionary<int, AccountingCategoryKind> kinds;
    private readonly List<BankRule> rules;

    private BankMatchFinder(List<(AccountingEntry, Dictionary<int, long>)> entries, List<AccountingProposal> proposals, IReadOnlyDictionary<int, AccountingCategoryKind> kinds, List<BankRule> rules)
    {
        this.entries = entries;
        this.proposals = proposals;
        this.kinds = kinds;
        this.rules = rules;
    }

    public static async Task<BankMatchFinder> Load(AppDbContext context, int clubId, IReadOnlyCollection<BankTransaction> lines, CancellationToken cancellationToken)
    {
        var kinds = await context.CategoryKinds(clubId, cancellationToken);
        var rules = await context.BankRules.AsNoTracking().Where(r => r.ClubId == clubId).OrderBy(r => r.Id).ToListAsync(cancellationToken);
        if (lines.Count == 0)
            return new BankMatchFinder([], [], kinds, rules);

        var accounts = lines.Select(l => l.MoneyAccountId).Distinct().ToList();
        var from = lines.Min(l => l.BookingDate).AddDays(-GetBankTransactions.MatchDays);
        var to = lines.Max(l => l.BookingDate).AddDays(GetBankTransactions.MatchDays);

        var matched = context.BankTransactions.Where(t => t.EntryId != null).Select(t => t.EntryId);
        var entries = await context.AccountingEntries.AsNoTracking().Include(e => e.Lines)
            .Where(e => e.ClubId == clubId && e.Status == Bookennis.Domain.Accounting.AccountingEntryStatus.Active && e.Date >= from && e.Date <= to
                        && (accounts.Contains(e.MoneyAccountId) || (e.ToMoneyAccountId != null && accounts.Contains(e.ToMoneyAccountId.Value)))
                        && !matched.Contains(e.Id))
            .ToListAsync(cancellationToken);
        var proposals = await context.AccountingProposals.AsNoTracking()
            .Where(p => p.ClubId == clubId && p.Status == AccountingProposalStatus.Open && p.Date >= from && p.Date <= to
                        && (accounts.Contains(p.MoneyAccountId) || (p.ToMoneyAccountId != null && accounts.Contains(p.ToMoneyAccountId.Value))))
            .ToListAsync(cancellationToken);

        return new BankMatchFinder(
            entries.Select(e => (e, accounts.ToDictionary(a => a, a => e.EffectOn(a, id => kinds[id])))).ToList(),
            proposals, kinds, rules);
    }

    public List<BankMatchDto> Matches(BankTransaction line)
    {
        var near = (DateOnly date) => Math.Abs(date.DayNumber - line.BookingDate.DayNumber) <= GetBankTransactions.MatchDays;
        var fromEntries = entries
            .Where(e => near(e.Entry.Date) && e.Effects.GetValueOrDefault(line.MoneyAccountId) == line.AmountCents)
            .OrderBy(e => Math.Abs(e.Entry.Date.DayNumber - line.BookingDate.DayNumber))
            .Select(e => new BankMatchDto { Kind = BankMatchKind.Entry, Id = e.Entry.Id, Number = e.Entry.Number, Date = e.Entry.Date, Text = e.Entry.Text });
        var fromProposals = proposals
            .Where(p => near(p.Date) && EffectOn(p, line.MoneyAccountId) == line.AmountCents)
            .OrderBy(p => Math.Abs(p.Date.DayNumber - line.BookingDate.DayNumber))
            .Select(p => new BankMatchDto { Kind = BankMatchKind.Proposal, Id = p.Id, Date = p.Date, Text = p.Text });
        return fromEntries.Concat(fromProposals).Take(5).ToList();
    }

    public BankRule? Rule(BankTransaction line) => rules.FirstOrDefault(r => r.Matches(line));

    private long EffectOn(AccountingProposal proposal, int moneyAccountId)
    {
        if (proposal.Kind == AccountingEntryKind.Transfer)
            return (proposal.MoneyAccountId == moneyAccountId ? -proposal.AmountCents : 0) + (proposal.ToMoneyAccountId == moneyAccountId ? proposal.AmountCents : 0);
        if (proposal.MoneyAccountId != moneyAccountId || proposal.CategoryId is not { } categoryId || !kinds.TryGetValue(categoryId, out var kind))
            return 0;
        return kind == AccountingCategoryKind.Income ? proposal.AmountCents : -proposal.AmountCents;
    }
}

internal static class BankMapper
{
    /// <summary>A money account the caller filters by must be one of the club.</summary>
    public static async Task EnsureAccountOfClub(AppDbContext context, int clubId, int? moneyAccountId, CancellationToken cancellationToken)
    {
        if (moneyAccountId is { } id && !await context.MoneyAccounts.AnyAsync(a => a.Id == id && a.ClubId == clubId, cancellationToken))
            throw new EntityNotFoundException(typeof(MoneyAccount), id);
    }

    /// <summary>A bank line goes back on the list when its entry was cancelled or no longer books its amount.</summary>
    public static async Task ReopenLinesNoLongerBooked(AppDbContext context, AccountingEntry entry, CancellationToken cancellationToken)
    {
        var lines = await context.BankTransactions.Where(t => t.ClubId == entry.ClubId && t.EntryId == entry.Id).ToListAsync(cancellationToken);
        if (lines.Count == 0)
            return;

        var kinds = await context.CategoryKinds(entry.ClubId, cancellationToken);
        foreach (var line in lines.Where(l => entry.EffectOn(l.MoneyAccountId, id => kinds[id]) != l.AmountCents))
            line.Reopen();
    }

    public static async Task Match(AppDbContext context, BankTransaction line, AccountingEntry entry, CancellationToken cancellationToken)
    {
        if (await context.BankTransactions.AnyAsync(t => t.EntryId == entry.Id && t.Id != line.Id, cancellationToken))
            throw new PreconditionException(AccountingErrorCode.AccountingEntryAlreadyMatched, [entry.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)], "The entry is matched with another bank line.");
        var kinds = await context.CategoryKinds(line.ClubId, cancellationToken);
        line.Match(entry, id => kinds[id]);
    }

    /// <summary>The entry a rule books a line as: the amount in the direction of the category (an income that went out is a refund).</summary>
    public static SaveAccountingEntryModel EntryFor(BankTransaction line, BankRule rule, AccountingCategoryKind kind)
    {
        var text = rule.Text ?? string.Join(" ", new[] { line.CounterpartyName, line.Reference }.Where(t => !string.IsNullOrWhiteSpace(t)));
        if (string.IsNullOrWhiteSpace(text))
            text = "-";
        return new SaveAccountingEntryModel
        {
            Date = line.BookingDate,
            Text = text.Length > AccountingEntry.MaxTextLength ? text[..AccountingEntry.MaxTextLength] : text,
            Kind = global::Bookennis.Shared.Controller.Accounting.AccountingEntryKind.Booking,
            MoneyAccountId = line.MoneyAccountId,
            Lines = [new SaveAccountingEntryLineModel { CategoryId = rule.CategoryId, CostCenterId = rule.CostCenterId, AmountCents = kind == AccountingCategoryKind.Income ? line.AmountCents : -line.AmountCents }],
            CounterpartyName = line.CounterpartyName is { } name && name.Length > AccountingEntry.MaxCounterpartyLength ? name[..AccountingEntry.MaxCounterpartyLength] : line.CounterpartyName,
        };
    }

    /// <summary>The balance of a money account at the end of <paramref name="date"/> in the books; <c>null</c> when no fiscal year contains it.</summary>
    public static async Task<long?> BalanceAt(AppDbContext context, int clubId, int moneyAccountId, DateOnly date, CancellationToken cancellationToken)
    {
        var year = await context.FiscalYears.AsNoTracking().Include(y => y.OpeningBalances)
            .SingleOrDefaultAsync(y => y.ClubId == clubId && y.StartDate <= date && y.EndDate >= date, cancellationToken);
        if (year is null)
            return null;

        var kinds = await context.CategoryKinds(clubId, cancellationToken);
        var entries = await context.AccountingEntries.AsNoTracking().Include(e => e.Lines)
            .Where(e => e.FiscalYearId == year.Id && e.Status == Bookennis.Domain.Accounting.AccountingEntryStatus.Active && e.Date <= date
                        && (e.MoneyAccountId == moneyAccountId || e.ToMoneyAccountId == moneyAccountId))
            .ToListAsync(cancellationToken);
        return year.OpeningBalanceCents(moneyAccountId) + entries.Sum(e => e.EffectOn(moneyAccountId, id => kinds[id]));
    }

    public static async Task<BankRule.RuleData> RuleData(AppDbContext context, int clubId, SaveBankRuleModel model, CancellationToken cancellationToken)
    {
        if (!await context.AccountingCategories.AnyAsync(c => c.Id == model.CategoryId && c.ClubId == clubId, cancellationToken))
            throw new EntityNotFoundException(typeof(AccountingCategory), model.CategoryId);
        if (model.CostCenterId is { } costCenterId && !await context.CostCenters.AnyAsync(c => c.Id == costCenterId && c.ClubId == clubId, cancellationToken))
            throw new EntityNotFoundException(typeof(CostCenter), costCenterId);
        if (model.MoneyAccountId is { } accountId && !await context.MoneyAccounts.AnyAsync(a => a.Id == accountId && a.ClubId == clubId, cancellationToken))
            throw new EntityNotFoundException(typeof(MoneyAccount), accountId);

        return new BankRule.RuleData(model.MatchText, model.MatchIban, (BankRuleDirection)model.Direction, model.CategoryId, model.CostCenterId, model.Text, model.MoneyAccountId);
    }

    public static BankRuleDto ToDto(BankRule rule) => new()
    {
        Id = rule.Id,
        MatchText = rule.MatchText,
        MatchIban = rule.MatchIban,
        Direction = (global::Bookennis.Shared.Controller.Accounting.BankRuleDirection)rule.Direction,
        CategoryId = rule.CategoryId,
        CostCenterId = rule.CostCenterId,
        Text = rule.Text,
        MoneyAccountId = rule.MoneyAccountId,
    };

    public static CsvColumns ToDomain(CsvColumnsDto dto) => new()
    {
        SkipRows = Math.Clamp(dto.SkipRows, 0, 100),
        Delimiter = dto.Delimiter is { Length: 1 } delimiter ? delimiter[0] : dto.Delimiter == "\\t" ? '\t' : ';',
        BookingDate = dto.BookingDate,
        ValueDate = dto.ValueDate,
        Amount = dto.Amount,
        Debit = dto.Debit,
        Credit = dto.Credit,
        DebitCreditMarker = dto.DebitCreditMarker,
        CounterpartyName = dto.CounterpartyName,
        CounterpartyIban = dto.CounterpartyIban,
        Reference = (dto.Reference ?? []).Distinct().Take(10).ToList(),
    };

    public static CsvColumnsDto ToDto(CsvColumns columns) => new()
    {
        SkipRows = columns.SkipRows,
        Delimiter = columns.Delimiter.ToString(),
        BookingDate = columns.BookingDate,
        ValueDate = columns.ValueDate,
        Amount = columns.Amount,
        Debit = columns.Debit,
        Credit = columns.Credit,
        DebitCreditMarker = columns.DebitCreditMarker,
        CounterpartyName = columns.CounterpartyName,
        CounterpartyIban = columns.CounterpartyIban,
        Reference = columns.Reference.ToList(),
    };
}
