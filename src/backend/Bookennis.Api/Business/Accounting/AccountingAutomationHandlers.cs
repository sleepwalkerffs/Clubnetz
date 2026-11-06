using System.Globalization;
using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingAutomationMode = Bookennis.Domain.Accounting.AccountingAutomationMode;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using AccountingEntryKind = Bookennis.Domain.Accounting.AccountingEntryKind;
using AccountingProposalStatus = Bookennis.Domain.Accounting.AccountingProposalStatus;
using AccountingSource = Bookennis.Domain.Accounting.AccountingSource;

namespace Bookennis.Api.Business.Accounting;

/// <summary>Which payments become entries by themselves, and into which money accounts.</summary>
public record GetAccountingAutomation(int ClubId) : IQuery<AccountingAutomationDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingAutomation, AccountingAutomationDto>
    {
        public async Task<AccountingAutomationDto> Handle(GetAccountingAutomation request, CancellationToken cancellationToken)
        {
            var settings = await context.AccountingSettings.AsNoTracking().SingleOrDefaultAsync(s => s.ClubId == request.ClubId, cancellationToken)
                           ?? throw new PreconditionException(AccountingErrorCode.AccountingNotSetUp, "The accounting is not set up.");
            var sources = await context.AccountingSourceSettings.AsNoTracking().Where(s => s.ClubId == request.ClubId).ToListAsync(cancellationToken);
            return AccountingAutomationMapper.ToDto(settings, sources, await AccountingAutomationMapper.StripeConnected(context, request.ClubId, cancellationToken));
        }
    }
}

/// <summary>
/// Switches sources of automatic entries on or off. A source that is on needs a category of the right kind and the money accounts its
/// payments go to; all of them must belong to the club.
/// </summary>
public record UpdateAccountingAutomation(int ClubId, SaveAccountingAutomationModel Model, DateTimeOffset Now) : ICommand<AccountingAutomationDto>
{
    public class Handler(AppDbContext context, IUserLanguageAccessor languageAccessor) : IRequestHandler<UpdateAccountingAutomation, AccountingAutomationDto>
    {
        public async Task<AccountingAutomationDto> Handle(UpdateAccountingAutomation request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var settings = await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var model = request.Model;
            var stripeConnected = await AccountingAutomationMapper.StripeConnected(context, request.ClubId, cancellationToken);

            var accountIds = new[] { model.OnlineMoneyAccountId, model.BankMoneyAccountId, model.CashMoneyAccountId, model.CardMoneyAccountId }
                .Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
            var accounts = await context.MoneyAccounts.Where(a => a.ClubId == request.ClubId && accountIds.Contains(a.Id)).ToListAsync(cancellationToken);
            if (accounts.Count != accountIds.Count)
                throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(MoneyAccount));

            var categoryIds = model.Sources.Where(s => s.CategoryId is not null).Select(s => s.CategoryId!.Value).Distinct().ToList();
            var categories = await context.AccountingCategories.Where(c => c.ClubId == request.ClubId && categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
            if (categories.Count != categoryIds.Count)
                throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(AccountingCategory));

            var existing = await context.AccountingSourceSettings.Where(s => s.ClubId == request.ClubId).ToListAsync(cancellationToken);
            var today = AccountingQueries.Today(request.Now);
            foreach (var input in model.Sources.DistinctBy(s => s.Source))
            {
                var source = (AccountingSource)input.Source;
                if (!Enum.IsDefined(source))
                    throw new ArgumentOutOfRangeException(nameof(request), source, "Unknown source.");

                var mode = (AccountingAutomationMode)input.Mode;
                if (mode != AccountingAutomationMode.Off)
                {
                    if (input.CategoryId is not { } categoryId || categories[categoryId].Kind != AccountingSourceSetting.CategoryKindOf(source))
                        throw new PreconditionException(AccountingErrorCode.AccountingAutomationIncomplete, "The source needs a category of the right kind.");
                    if (!AccountingAutomationMapper.HasAccounts(source, model, stripeConnected))
                        throw new PreconditionException(AccountingErrorCode.AccountingAutomationIncomplete, "The source needs its money accounts.");
                }

                var setting = existing.SingleOrDefault(s => s.Source == source);
                if (setting is null)
                {
                    setting = new AccountingSourceSetting(request.ClubId, source);
                    context.Add(setting);
                    existing.Add(setting);
                }
                setting.Change(mode, input.Since ?? today, input.CategoryId);
            }

            settings.SetAutomationAccounts(model.OnlineMoneyAccountId, model.BankMoneyAccountId, model.CashMoneyAccountId, model.CardMoneyAccountId,
                languageAccessor.GetCultureInfo().TwoLetterISOLanguageName == "de");
            await context.SaveChangesAsync(cancellationToken);
            return AccountingAutomationMapper.ToDto(settings, existing, stripeConnected);
        }
    }
}

/// <summary>The open suggestions (or the ones of a status), newest first.</summary>
public record GetAccountingProposals(int ClubId, AccountingProposalStatus Status) : IQuery<List<AccountingProposalDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingProposals, List<AccountingProposalDto>>
    {
        public async Task<List<AccountingProposalDto>> Handle(GetAccountingProposals request, CancellationToken cancellationToken)
        {
            var proposals = await context.AccountingProposals.AsNoTracking()
                .Where(p => p.ClubId == request.ClubId && p.Status == request.Status)
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
                .Take(500)
                .ToListAsync(cancellationToken);
            return proposals.Select(AccountingAutomationMapper.ToDto).ToList();
        }
    }
}

/// <summary>
/// Collects what was paid since the last time into suggestions (each payment once), and books the suggestions of the sources that book
/// by themselves. Run by the hourly job and when the treasurer asks for it.
/// </summary>
public record SyncAccountingProposals(int ClubId, DateTimeOffset Now) : ICommand<SyncAccountingProposalsResult>
{
    public class Handler(AppDbContext context, IStripeGateway stripeGateway, ILogger<SyncAccountingProposals> logger) : IRequestHandler<SyncAccountingProposals, SyncAccountingProposalsResult>
    {
        public async Task<SyncAccountingProposalsResult> Handle(SyncAccountingProposals request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var settings = await context.GetAccountingSettings(request.ClubId, cancellationToken);
            var sources = await context.AccountingSourceSettings.Where(s => s.ClubId == request.ClubId && s.Mode != AccountingAutomationMode.Off).ToListAsync(cancellationToken);

            var collector = new AccountingProposalCollector(context, stripeGateway, settings, request.Now);
            var candidates = new List<(AccountingSource Source, string Key, AccountingProposal.ProposalData Data)>();
            var stripeFailed = false;
            foreach (var source in sources)
            {
                try
                {
                    candidates.AddRange((await collector.Collect(source, cancellationToken)).Select(c => (source.Source, c.Key, c.Data)));
                }
                catch (Exception e) when (source.Source == AccountingSource.Stripe && e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "Stripe could not be asked for the accounting of club {ClubId}", request.ClubId);
                    stripeFailed = true;
                }
            }

            // Each payment once: what was suggested before (open, booked or dismissed) is left alone
            var keys = candidates.Select(c => c.Key).ToList();
            var known = (await context.AccountingProposals.Where(p => p.ClubId == request.ClubId && keys.Contains(p.SourceKey)).Select(p => p.SourceKey).ToListAsync(cancellationToken)).ToHashSet();
            var created = 0;
            foreach (var candidate in candidates.Where(c => known.Add(c.Key)))
            {
                context.Add(new AccountingProposal(request.ClubId, candidate.Source, candidate.Key, candidate.Data, request.Now));
                created++;
            }
            await context.SaveChangesAsync(cancellationToken);

            // The sources that book by themselves: every open suggestion (also the ones that failed before, e.g. without fiscal year)
            var booking = sources.Where(s => s.Mode == AccountingAutomationMode.Book).Select(s => s.Source).ToList();
            var toBook = await context.AccountingProposals
                .Where(p => p.ClubId == request.ClubId && p.Status == AccountingProposalStatus.Open && booking.Contains(p.Source))
                .OrderBy(p => p.Date).ThenBy(p => p.Id)
                .ToListAsync(cancellationToken);
            var booked = 0;
            foreach (var proposal in toBook)
            {
                if (await AccountingProposalBooking.TryBook(context, proposal, null, null, request.Now, cancellationToken))
                    booked++;
            }
            await context.SaveChangesAsync(cancellationToken);

            var open = await context.AccountingProposals.CountAsync(p => p.ClubId == request.ClubId && p.Status == AccountingProposalStatus.Open, cancellationToken);
            return new SyncAccountingProposalsResult { Created = created, Booked = booked, Open = open, StripeFailed = stripeFailed };
        }
    }
}

/// <summary>Books a suggestion: as suggested, or as the treasurer changed it (<see cref="BookAccountingProposalModel.Entry"/>).</summary>
public record BookAccountingProposal(int ClubId, int UserId, int ProposalId, BookAccountingProposalModel Model, DateTimeOffset Now) : ICommand<AccountingEntryDetailDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<BookAccountingProposal, AccountingEntryDetailDto>
    {
        public async Task<AccountingEntryDetailDto> Handle(BookAccountingProposal request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var proposal = await context.AccountingProposals.SingleRequiredAsync(p => p.Id == request.ProposalId && p.ClubId == request.ClubId, cancellationToken);
            proposal.EnsureOpen();

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var entry = await AccountingProposalBooking.Book(context, proposal, request.Model.Entry, memberId, request.Now, cancellationToken);
            return await AccountingEntryDetails.Load(context, request.ClubId, entry.Id, cancellationToken);
        }
    }
}

/// <summary>The treasurer decides a suggestion is no entry (e.g. booked by hand already). It is never suggested again.</summary>
public record DismissAccountingProposal(int ClubId, int ProposalId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DismissAccountingProposal>
    {
        public async Task<Unit> Handle(DismissAccountingProposal request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var proposal = await context.AccountingProposals.SingleRequiredAsync(p => p.Id == request.ProposalId && p.ClubId == request.ClubId, cancellationToken);
            proposal.Dismiss();
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>Every hour: the suggestions of every club with a source of automatic entries that is on, one club after the other.</summary>
public record SyncAllAccountingProposals : ICommand
{
    public class Handler(AppDbContext context, IMediator mediator, ILogger<SyncAllAccountingProposals> logger) : IRequestHandler<SyncAllAccountingProposals>
    {
        public async Task<Unit> Handle(SyncAllAccountingProposals request, CancellationToken cancellationToken)
        {
            var clubIds = await context.AccountingSourceSettings.AsNoTracking()
                .Where(s => s.Mode != AccountingAutomationMode.Off)
                .Select(s => s.ClubId).Distinct()
                .ToListAsync(cancellationToken);

            foreach (var clubId in clubIds)
            {
                try
                {
                    await mediator.Send(new SyncAccountingProposals(clubId, DateTimeOffset.UtcNow), cancellationToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "The automatic entries of club {ClubId} failed", clubId);
                }
            }

            return default;
        }
    }
}

public class AccountingAutomationJob(IMediator mediator)
{
    public const string JobId = "accounting-automation";
    public const string Schedule = "41 * * * *";

    public Task Run() => mediator.Send(new SyncAllAccountingProposals());
}

internal static class AccountingProposalBooking
{
    /// <summary>Books <paramref name="proposal"/>; <paramref name="changed"/> is the entry as the treasurer changed it.</summary>
    public static async Task<AccountingEntry> Book(AppDbContext context, AccountingProposal proposal, SaveAccountingEntryModel? changed, int? memberId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entry = await AccountingEntryCreation.Create(context, proposal.ClubId, memberId, changed ?? AccountingEntryCreation.ToModel(proposal.ToEntryData()), now, cancellationToken);
        proposal.Booked(entry);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    /// <summary>Books it by itself; when a rule of the books stops it (no open fiscal year, archived account), it stays open with the reason.</summary>
    public static async Task<bool> TryBook(AppDbContext context, AccountingProposal proposal, SaveAccountingEntryModel? changed, int? memberId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await Book(context, proposal, changed, memberId, now, cancellationToken);
            return true;
        }
        catch (PreconditionException e)
        {
            proposal.CouldNotBook(e.ErrorCode ?? nameof(PreconditionException));
            return false;
        }
    }
}

internal static class AccountingAutomationMapper
{
    public static Task<bool> StripeConnected(AppDbContext context, int clubId, CancellationToken cancellationToken)
        => context.ClubPaymentAccounts.AnyAsync(a => a.ClubId == clubId, cancellationToken);

    /// <summary>The money accounts a source needs: online payments go to the Stripe balance (when the club takes them), bank transfers and cash to their accounts.</summary>
    public static bool HasAccounts(AccountingSource source, SaveAccountingAutomationModel model, bool stripeConnected) => source switch
    {
        AccountingSource.MembershipFees => model.BankMoneyAccountId is not null && model.CashMoneyAccountId is not null && (!stripeConnected || model.OnlineMoneyAccountId is not null),
        AccountingSource.CourtSales => model.OnlineMoneyAccountId is not null,
        AccountingSource.Clubhouse => model.CashMoneyAccountId is not null && model.CardMoneyAccountId is not null,
        AccountingSource.Stripe => model.OnlineMoneyAccountId is not null && model.BankMoneyAccountId is not null,
        _ => false,
    };

    public static AccountingAutomationDto ToDto(AccountingSettings settings, IReadOnlyCollection<AccountingSourceSetting> sources, bool stripeConnected) => new()
    {
        OnlineMoneyAccountId = settings.OnlineMoneyAccountId,
        BankMoneyAccountId = settings.BankMoneyAccountId,
        CashMoneyAccountId = settings.CashMoneyAccountId,
        CardMoneyAccountId = settings.CardMoneyAccountId,
        StripeConnected = stripeConnected,
        Sources = Enum.GetValues<AccountingSource>().Select(source => sources.SingleOrDefault(s => s.Source == source) is { } setting
            ? new AccountingSourceSettingDto
            {
                Source = (global::Bookennis.Shared.Controller.Accounting.AccountingSource)source,
                Mode = (global::Bookennis.Shared.Controller.Accounting.AccountingAutomationMode)setting.Mode,
                Since = setting.Since,
                CategoryId = setting.CategoryId,
            }
            : new AccountingSourceSettingDto { Source = (global::Bookennis.Shared.Controller.Accounting.AccountingSource)source }).ToList(),
    };

    public static AccountingProposalDto ToDto(AccountingProposal proposal) => new()
    {
        Id = proposal.Id,
        Source = (global::Bookennis.Shared.Controller.Accounting.AccountingSource)proposal.Source,
        Date = proposal.Date,
        Text = proposal.Text,
        Kind = (global::Bookennis.Shared.Controller.Accounting.AccountingEntryKind)proposal.Kind,
        MoneyAccountId = proposal.MoneyAccountId,
        ToMoneyAccountId = proposal.ToMoneyAccountId,
        CategoryId = proposal.CategoryId,
        AmountCents = proposal.AmountCents,
        CounterpartyMemberId = proposal.CounterpartyMemberId,
        CounterpartyName = proposal.CounterpartyName,
        Status = (global::Bookennis.Shared.Controller.Accounting.AccountingProposalStatus)proposal.Status,
        EntryId = proposal.EntryId,
        Problem = proposal.Problem,
    };
}

/// <summary>Turns what was paid in Clubnetz into suggested entries, per source. Pure reading: the caller adds what is new.</summary>
internal sealed class AccountingProposalCollector(AppDbContext context, IStripeGateway stripeGateway, AccountingSettings settings, DateTimeOffset now)
{
    /// <summary>Stripe is asked at most this far back (its fees and payouts of older days were collected before).</summary>
    private const int StripeOverlapDays = 3;

    private readonly DateOnly today = AccountingQueries.Today(now);
    private bool German => settings.GermanTexts;

    public sealed record Candidate(string Key, AccountingProposal.ProposalData Data);

    public Task<List<Candidate>> Collect(AccountingSourceSetting source, CancellationToken cancellationToken) => source.Source switch
    {
        AccountingSource.MembershipFees => MembershipFees(source, cancellationToken),
        AccountingSource.CourtSales => CourtSales(source, cancellationToken),
        AccountingSource.Clubhouse => Clubhouse(source, cancellationToken),
        AccountingSource.Stripe => Stripe(source, cancellationToken),
        _ => Task.FromResult(new List<Candidate>()),
    };

    private static DateTimeOffset StartOfDay(DateOnly date)
        => new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), ClubTimeZone.Instance), TimeSpan.Zero);

    private static DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(ClubTimeZone.ToLocal(value));

    private static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    // ---------- membership fees ----------

    private async Task<List<Candidate>> MembershipFees(AccountingSourceSetting source, CancellationToken cancellationToken)
    {
        var since = StartOfDay(source.Since);
        var orders = await context.MembershipOrders.AsNoTracking()
            .Where(o => o.ClubId == settings.ClubId && o.PaidAt != null && o.PaidAt >= since && o.AmountCents > 0 && o.PaymentMethod != null)
            .Select(o => new { o.Id, o.PlanName, o.AmountCents, o.PaymentMethod, o.PaidAt, o.Status, o.PayerMemberId })
            .ToListAsync(cancellationToken);
        var names = await context.MemberNames(settings.ClubId, orders.Select(o => o.PayerMemberId), cancellationToken);

        var candidates = new List<Candidate>();
        foreach (var order in orders)
        {
            var account = order.PaymentMethod switch
            {
                MembershipPaymentMethod.Online => settings.OnlineMoneyAccountId,
                MembershipPaymentMethod.Cash => settings.CashMoneyAccountId,
                _ => settings.BankMoneyAccountId,
            };
            if (account is null)
                continue;

            var payer = order.PayerMemberId is { } payerId ? names.GetValueOrDefault(payerId) : null;
            var text = (German ? "Mitgliedsbeitrag " : "Membership fee ") + order.PlanName;
            candidates.Add(new Candidate(AccountingProposal.Key("membership-order", order.Id),
                new AccountingProposal.ProposalData(LocalDate(order.PaidAt!.Value), text, AccountingEntryKind.Booking, account.Value, null, source.CategoryId, order.AmountCents, order.PayerMemberId, payer)));

            // Online payments given back or taken back by the bank of the member (dispute): the money left the Stripe balance
            if (order.PaymentMethod == MembershipPaymentMethod.Online && order.Status is MembershipOrderStatus.Refunded or MembershipOrderStatus.Disputed)
            {
                var refund = order.Status == MembershipOrderStatus.Refunded ? (German ? "Rückerstattung " : "Refund ") : (German ? "Zahlungseinspruch " : "Dispute ");
                candidates.Add(new Candidate(AccountingProposal.Key("membership-order", order.Id, "refund"),
                    new AccountingProposal.ProposalData(today, refund + text, AccountingEntryKind.Booking, account.Value, null, source.CategoryId, -order.AmountCents, order.PayerMemberId, payer)));
            }
        }

        return candidates;
    }

    // ---------- court sales ----------

    private async Task<List<Candidate>> CourtSales(AccountingSourceSetting source, CancellationToken cancellationToken)
    {
        if (settings.OnlineMoneyAccountId is not { } account)
            return [];

        var since = StartOfDay(source.Since);
        var purchases = await (
            from purchase in context.CourtPurchases.AsNoTracking()
            join court in context.Courts on purchase.CourtId equals court.Id
            where purchase.ClubId == settings.ClubId && purchase.PaidAt != null && purchase.PaidAt >= since
                  && purchase.PaymentMethod == CourtPurchasePaymentMethod.Online && purchase.AmountDueCents > 0
            select new { purchase.Id, purchase.AmountDueCents, purchase.PaidAt, purchase.Status, purchase.MemberId, purchase.Interval, CourtName = court.Name }
        ).ToListAsync(cancellationToken);
        var names = await context.MemberNames(settings.ClubId, purchases.Select(p => p.MemberId), cancellationToken);

        var candidates = new List<Candidate>();
        foreach (var purchase in purchases)
        {
            var member = purchase.MemberId is { } memberId ? names.GetValueOrDefault(memberId) : null;
            var start = ClubTimeZone.ToLocal(purchase.Interval.From);
            var text = $"{(German ? "Platzbuchung" : "Court booking")} {purchase.CourtName} {start.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}";
            candidates.Add(new Candidate(AccountingProposal.Key("court-purchase", purchase.Id),
                new AccountingProposal.ProposalData(LocalDate(purchase.PaidAt!.Value), text, AccountingEntryKind.Booking, account, null, source.CategoryId, purchase.AmountDueCents, purchase.MemberId, member)));

            if (purchase.Status is CourtPurchaseStatus.Refunded or CourtPurchaseStatus.Disputed)
            {
                var refund = purchase.Status == CourtPurchaseStatus.Refunded ? (German ? "Rückerstattung " : "Refund ") : (German ? "Zahlungseinspruch " : "Dispute ");
                candidates.Add(new Candidate(AccountingProposal.Key("court-purchase", purchase.Id, "refund"),
                    new AccountingProposal.ProposalData(today, refund + text, AccountingEntryKind.Booking, account, null, source.CategoryId, -purchase.AmountDueCents, purchase.MemberId, member)));
            }
        }

        return candidates;
    }

    // ---------- clubhouse ----------

    /// <summary>One entry per day and way of paying, for the days that are over (the totals of today still grow).</summary>
    private async Task<List<Candidate>> Clubhouse(AccountingSourceSetting source, CancellationToken cancellationToken)
    {
        var since = StartOfDay(source.Since);
        var until = StartOfDay(today);
        var tabs = await context.ClubhouseTabs.AsNoTracking()
            .Where(t => t.ClubId == settings.ClubId && t.Status == ClubhouseTabStatus.Paid && t.PaidAt != null && t.PaidAt >= since && t.PaidAt < until
                        && t.PaymentMethod != null && t.PaymentMethod != ClubhousePaymentMethod.Other)
            .Select(t => new { t.PaidAt, t.PaymentMethod, Total = t.Lines.Sum(l => l.UnitPriceCents * l.Quantity) })
            .ToListAsync(cancellationToken);

        var candidates = new List<Candidate>();
        foreach (var day in tabs.GroupBy(t => (Date: LocalDate(t.PaidAt!.Value), t.PaymentMethod!.Value)))
        {
            var total = day.Sum(t => (long)t.Total);
            var (account, way, key) = day.Key.Value switch
            {
                ClubhousePaymentMethod.Cash => (settings.CashMoneyAccountId, German ? "bar" : "cash", "cash"),
                ClubhousePaymentMethod.Card => (settings.CardMoneyAccountId, German ? "Karte" : "card", "card"),
                _ => (settings.CardMoneyAccountId, "online (SumUp)", "online"),
            };
            if (account is null || total <= 0 || total > AccountingEntry.MaxAmountCents)
                continue;

            var text = $"{(German ? "Clubhaus" : "Clubhouse")} {Date(day.Key.Date)} {way}";
            candidates.Add(new Candidate(AccountingProposal.Key("clubhouse", day.Key.Date, key),
                new AccountingProposal.ProposalData(day.Key.Date, text, AccountingEntryKind.Booking, account.Value, null, source.CategoryId, (int)total, null, null)));
        }

        return candidates;
    }

    // ---------- Stripe ----------

    /// <summary>The fees of each day that is over (an expense on the Stripe balance) and every payout (a transfer to the bank).</summary>
    private async Task<List<Candidate>> Stripe(AccountingSourceSetting source, CancellationToken cancellationToken)
    {
        if (settings.OnlineMoneyAccountId is not { } online || settings.BankMoneyAccountId is not { } bank || !stripeGateway.IsConfigured)
            return [];
        var accountId = await context.ClubPaymentAccounts.Where(a => a.ClubId == settings.ClubId).Select(a => a.StripeAccountId).FirstOrDefaultAsync(cancellationToken);
        if (accountId is null)
            return [];

        // From the last day collected (a few days back, payouts can be created late), at most a year back
        var lastDate = await context.AccountingProposals
            .Where(p => p.ClubId == settings.ClubId && p.Source == AccountingSource.Stripe)
            .MaxAsync(p => (DateOnly?)p.Date, cancellationToken);
        var from = new[] { source.Since, (lastDate ?? source.Since).AddDays(-StripeOverlapDays), today.AddYears(-1) }.Max();
        if (lastDate is null)
            from = new[] { source.Since, today.AddYears(-1) }.Max();
        var transactions = await stripeGateway.ListBalanceTransactions(accountId, StartOfDay(from), now, cancellationToken);

        var candidates = new List<Candidate>();
        foreach (var day in transactions.GroupBy(t => LocalDate(t.Created)).Where(d => d.Key >= source.Since && d.Key < today))
        {
            var fees = day.Sum(t => t.FeeCents + (t.Type == StripeBalanceTransaction.TypeStripeFee ? -t.AmountCents : 0));
            if (fees is > 0 and <= AccountingEntry.MaxAmountCents)
            {
                candidates.Add(new Candidate(AccountingProposal.Key("stripe-fees", day.Key),
                    new AccountingProposal.ProposalData(day.Key, $"{(German ? "Stripe-Gebühren" : "Stripe fees")} {Date(day.Key)}", AccountingEntryKind.Booking, online, null, source.CategoryId, (int)fees, null, "Stripe")));
            }
        }

        foreach (var payout in transactions.Where(t => t.Type is StripeBalanceTransaction.TypePayout or StripeBalanceTransaction.TypePayoutFailure))
        {
            var date = LocalDate(payout.Created);
            var amount = Math.Abs(payout.AmountCents);
            if (date < source.Since || amount == 0 || amount > AccountingEntry.MaxAmountCents)
                continue;

            var failed = payout.Type == StripeBalanceTransaction.TypePayoutFailure;
            var text = failed ? (German ? "Stripe-Auszahlung fehlgeschlagen" : "Stripe payout failed") : (German ? "Stripe-Auszahlung" : "Stripe payout");
            candidates.Add(new Candidate(AccountingProposal.Key(failed ? "stripe-payout-failure" : "stripe-payout", payout.SourceId ?? payout.Id),
                new AccountingProposal.ProposalData(date, text, AccountingEntryKind.Transfer, failed ? bank : online, failed ? online : bank, null, (int)amount, null, "Stripe")));
        }

        return candidates;
    }
}
