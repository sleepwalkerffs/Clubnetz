using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Accounting;

public class FiscalYearTests
{
    private const int ClubId = 7;
    private static readonly DateOnly Today = new(2026, 11, 6);
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private static void Rejected(Action act, AccountingErrorCode code)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(code.ToString());

    [Fact]
    public void CalendarYear_IsLabelledWithItsYear()
    {
        var year = FiscalYear.First(ClubId, 1, 2026, Today);

        year.StartDate.Should().Be(new DateOnly(2026, 1, 1));
        year.EndDate.Should().Be(new DateOnly(2026, 12, 31));
        year.Label.Should().Be("2026");
        year.IsOpen.Should().BeTrue();
        year.NextEntryNumber.Should().Be(1);
    }

    [Fact]
    public void YearFromOctober_IsLabelledWithBothYears_AndEndsInSeptember()
    {
        var year = FiscalYear.First(ClubId, 10, 2025, Today);

        year.EndDate.Should().Be(new DateOnly(2026, 9, 30));
        year.Label.Should().Be("2025/26");
        year.Contains(new DateOnly(2026, 9, 30)).Should().BeTrue();
        year.Contains(new DateOnly(2026, 10, 1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 2026)]
    [InlineData(13, 2026)]
    [InlineData(1, 2015)]
    [InlineData(1, 2028)]
    public void FirstYear_MustBeAPlausibleStart(int month, int year)
        => Rejected(() => FiscalYear.First(ClubId, month, year, Today), AccountingErrorCode.AccountingFiscalYearStartInvalid);

    [Fact]
    public void NextYear_StartsTheDayAfter_ButNotMoreThanAYearAhead()
    {
        var year = FiscalYear.First(ClubId, 1, 2026, Today);
        var next = year.CreateNext(Today);

        next.StartDate.Should().Be(new DateOnly(2027, 1, 1));
        Rejected(() => next.CreateNext(Today), AccountingErrorCode.AccountingFiscalYearTooFarAhead);
        next.CreateNext(Today, closing: true).StartDate.Should().Be(next.EndDate.AddDays(1));
    }

    [Fact]
    public void Closing_NeedsTheYearBeforeClosed_AndLocksTheYear()
    {
        var previous = FiscalYear.First(ClubId, 1, 2025, Today);
        var year = previous.CreateNext(Today);

        Rejected(() => year.Close(previous, 1, Now), AccountingErrorCode.AccountingPreviousYearOpen);

        previous.Close(null, 1, Now);
        year.Close(previous, 1, Now);

        year.IsOpen.Should().BeFalse();
        year.ClosedByMemberId.Should().Be(1);
        Rejected(() => year.TakeEntryNumber(), AccountingErrorCode.AccountingFiscalYearClosed);
    }

    [Fact]
    public void Reopening_NeedsTheNextYearOpen_AndDropsTheAudit()
    {
        var year = FiscalYear.First(ClubId, 1, 2025, Today);
        var next = year.CreateNext(Today);
        year.Close(null, 1, Now);
        year.RecordAudit(FiscalYearAuditStatus.Audited, "All fine", 5, Now);
        next.Close(year, 1, Now);

        Rejected(() => year.Reopen(next), AccountingErrorCode.AccountingNextYearClosed);

        next.Reopen(null);
        year.Reopen(next);

        year.IsOpen.Should().BeTrue();
        year.AuditStatus.Should().Be(FiscalYearAuditStatus.NotAudited);
        year.AuditComment.Should().BeNull();
        year.AuditedByMemberId.Should().BeNull();
    }

    [Fact]
    public void Audit_IsOnlyForClosedYears()
    {
        var year = FiscalYear.First(ClubId, 1, 2026, Today);

        Rejected(() => year.RecordAudit(FiscalYearAuditStatus.Audited, null, 5, Now), AccountingErrorCode.AccountingFiscalYearNotClosed);

        year.Close(null, 1, Now);
        year.RecordAudit(FiscalYearAuditStatus.AuditedWithRemarks, "  Receipt 12 is missing  ", 5, Now);

        year.AuditStatus.Should().Be(FiscalYearAuditStatus.AuditedWithRemarks);
        year.AuditComment.Should().Be("Receipt 12 is missing");
        year.AuditedByMemberId.Should().Be(5);
        year.AuditedAt.Should().Be(Now);
    }

    [Fact]
    public void OpeningBalances_OfAccountsTheYearBeforeUsed_AreCarried_OthersSetByHand()
    {
        var year = FiscalYear.First(ClubId, 1, 2026, Today);
        year.SetOpeningBalance(1, 150_000);
        year.SetOpeningBalance(1, 160_000);
        year.SetOpeningBalance(2, -500);

        year.OpeningBalanceCents(1).Should().Be(160_000);
        year.OpeningBalanceCents(2).Should().Be(-500);
        year.OpeningBalanceCents(3).Should().Be(0);

        year.CarryOver(new Dictionary<int, int> { [1] = 90_000, [3] = 1_000 });

        year.OpeningBalancesCarried.Should().BeTrue();
        year.OpeningBalances.Should().HaveCount(3);
        year.OpeningBalanceCents(1).Should().Be(90_000);
        year.OpeningBalanceCents(2).Should().Be(-500);
        year.OpeningBalanceCents(3).Should().Be(1_000);
        Rejected(() => year.SetOpeningBalance(1, 0), AccountingErrorCode.AccountingOpeningBalancesCarried);
        year.SetOpeningBalance(2, -700);

        // The year before no longer uses account 3: its carried balance goes, the one set by hand stays
        year.CarryOver(new Dictionary<int, int> { [1] = 95_000 });
        year.OpeningBalances.Select(b => (b.MoneyAccountId, b.AmountCents, b.IsCarried)).Should().BeEquivalentTo([(1, 95_000, true), (2, -700, false)]);
    }

    [Fact]
    public void EntryNumbers_CountUp()
    {
        var year = FiscalYear.First(ClubId, 1, 2026, Today);

        year.TakeEntryNumber().Should().Be(1);
        year.TakeEntryNumber().Should().Be(2);
        year.NextEntryNumber.Should().Be(3);
    }
}

public class AccountingEntryTests
{
    private const int Bank = 1;
    private const int Cash = 2;
    private const int Fees = 10;
    private const int Balls = 20;
    private static readonly DateOnly Today = new(2026, 11, 6);
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private static AccountingCategoryKind KindOf(int categoryId) => categoryId == Fees ? AccountingCategoryKind.Income : AccountingCategoryKind.Expense;

    private static FiscalYear Year() => FiscalYear.First(7, 1, 2026, Today);

    private static AccountingEntry.EntryData Booking(params (int CategoryId, int AmountCents)[] lines)
        => new(new DateOnly(2026, 3, 1), "Membership fee Huber", AccountingEntryKind.Booking, Bank, null, null,
            lines.Select(l => new AccountingEntry.LineData(l.CategoryId, null, l.AmountCents)).ToList(), null, "  Anna Huber ");

    private static AccountingEntry.EntryData Transfer(int amountCents, int to = Cash)
        => new(new DateOnly(2026, 3, 2), "Cash for the clubhouse", AccountingEntryKind.Transfer, Bank, to, amountCents, [], null, null);

    private static void Rejected(Action act, AccountingErrorCode code)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(code.ToString());

    [Fact]
    public void Booking_IsNumbered_AndSplitLinesMoveTheMoneyAccount()
    {
        var year = Year();
        var first = new AccountingEntry(year, Booking((Fees, 12_000)), [], 3, Now);
        var second = new AccountingEntry(year, Booking((Fees, 12_000), (Balls, 2_500)), [41], 3, Now);

        first.Number.Should().Be(1);
        second.Number.Should().Be(2);
        second.CounterpartyName.Should().Be("Anna Huber");
        second.EffectOn(Bank, KindOf).Should().Be(9_500);
        second.EffectOn(Cash, KindOf).Should().Be(0);
        second.Changes.Should().ContainSingle().Which.Kind.Should().Be(AccountingEntryChangeKind.Created);
        AccountingEntry.EntrySnapshot.Read(second.Changes[0].Snapshot)!.ReceiptIds.Should().Equal(41);
    }

    [Fact]
    public void Refund_IsANegativeLine()
    {
        var entry = new AccountingEntry(Year(), Booking((Fees, -3_000)), [], null, Now);

        entry.EffectOn(Bank, KindOf).Should().Be(-3_000);
    }

    [Fact]
    public void Transfer_TakesFromOneAccountAndAddsToTheOther()
    {
        var entry = new AccountingEntry(Year(), Transfer(20_000), [], null, Now);

        entry.EffectOn(Bank, KindOf).Should().Be(-20_000);
        entry.EffectOn(Cash, KindOf).Should().Be(20_000);
    }

    [Fact]
    public void InvalidEntries_AreRejected()
    {
        var year = Year();

        Rejected(() => _ = new AccountingEntry(year, Booking(), [], null, Now), AccountingErrorCode.AccountingLinesInvalid);
        Rejected(() => _ = new AccountingEntry(year, Booking((Fees, 0)), [], null, Now), AccountingErrorCode.AccountingAmountInvalid);
        Rejected(() => _ = new AccountingEntry(year, Booking((Fees, AccountingEntry.MaxAmountCents + 1)), [], null, Now), AccountingErrorCode.AccountingAmountInvalid);
        Rejected(() => _ = new AccountingEntry(year, Booking((Fees, 100)) with { Text = " " }, [], null, Now), AccountingErrorCode.AccountingTextInvalid);
        Rejected(() => _ = new AccountingEntry(year, Booking((Fees, 100)) with { Date = new DateOnly(2027, 1, 1) }, [], null, Now), AccountingErrorCode.AccountingDateOutsideFiscalYear);
        Rejected(() => _ = new AccountingEntry(year, Transfer(100, to: Bank), [], null, Now), AccountingErrorCode.AccountingTransferInvalid);
        Rejected(() => _ = new AccountingEntry(year, Transfer(-100), [], null, Now), AccountingErrorCode.AccountingTransferInvalid);
        Rejected(() => _ = new AccountingEntry(year, Transfer(100) with { Lines = [new AccountingEntry.LineData(Fees, null, 100)] }, [], null, Now), AccountingErrorCode.AccountingTransferInvalid);

        // A rejected entry doesn't use up a number
        year.NextEntryNumber.Should().Be(1);
    }

    [Fact]
    public void Update_KeepsEveryVersion_AndTheDateStaysInTheYear()
    {
        var year = Year();
        var entry = new AccountingEntry(year, Booking((Fees, 12_000)), [], 3, Now);

        entry.Update(year, Booking((Fees, 13_000)) with { Text = "Membership fee Huber 2026" }, [7], 4, Now.AddHours(1));

        entry.Text.Should().Be("Membership fee Huber 2026");
        entry.Lines.Should().ContainSingle().Which.AmountCents.Should().Be(13_000);
        entry.Changes.Select(c => c.Kind).Should().Equal(AccountingEntryChangeKind.Created, AccountingEntryChangeKind.Changed);
        AccountingEntry.EntrySnapshot.Read(entry.Changes[0].Snapshot)!.Lines.Single().AmountCents.Should().Be(12_000);
        entry.Changes[1].ByMemberId.Should().Be(4);
        Rejected(() => entry.Update(year, Booking((Fees, 1)) with { Date = new DateOnly(2025, 12, 31) }, [], 4, Now), AccountingErrorCode.AccountingDateOutsideFiscalYear);
    }

    [Fact]
    public void CancelledEntry_NoLongerCounts_AndCantBeChanged()
    {
        var year = Year();
        var entry = new AccountingEntry(year, Booking((Fees, 12_000)), [], 3, Now);

        entry.Cancel(year, " Booked twice ", 3, Now);

        entry.IsActive.Should().BeFalse();
        entry.CancelReason.Should().Be("Booked twice");
        entry.EffectOn(Bank, KindOf).Should().Be(0);
        entry.Changes[^1].Kind.Should().Be(AccountingEntryChangeKind.Cancelled);
        Rejected(() => entry.Update(year, Booking((Fees, 1)), [], 3, Now), AccountingErrorCode.AccountingEntryCancelled);
        Rejected(() => entry.Cancel(year, null, 3, Now), AccountingErrorCode.AccountingEntryCancelled);
    }

    [Fact]
    public void EntryOfAClosedYear_CantBeChangedOrCancelled()
    {
        var year = Year();
        var entry = new AccountingEntry(year, Booking((Fees, 12_000)), [], 3, Now);
        year.Close(null, 3, Now);

        Rejected(() => entry.Update(year, Booking((Fees, 1)), [], 3, Now), AccountingErrorCode.AccountingFiscalYearClosed);
        Rejected(() => entry.Cancel(year, null, 3, Now), AccountingErrorCode.AccountingFiscalYearClosed);
    }
}

public class AccountingMasterDataTests
{
    [Theory]
    [InlineData("AT61 1904 3002 3457 3201", "AT611904300234573201")]
    [InlineData("de89370400440532013000", "DE89370400440532013000")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Iban_IsNormalized(string? input, string? expected)
        => IbanNumber.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("AT61 1904 3002 3457 3202")]
    [InlineData("AT61")]
    [InlineData("1234567890123456")]
    public void InvalidIban_IsRejected(string input)
        => FluentActions.Invoking(() => IbanNumber.Normalize(input)).Should().Throw<PreconditionException>()
            .Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingIbanInvalid));

    [Fact]
    public void OnlyBankAccounts_HaveAnIban()
    {
        var cash = new MoneyAccount(7, new MoneyAccount.AccountData("Kassa", MoneyAccountKind.Cash, "AT611904300234573201", 0));

        cash.Iban.Should().BeNull();
        IbanNumber.Format("AT611904300234573201").Should().Be("AT61 1904 3002 3457 3201");
    }

    [Fact]
    public void UsedCategory_StaysIncomeOrExpense()
    {
        var category = new AccountingCategory(7, new AccountingCategory.CategoryData(AccountingCategoryKind.Income, "4000", "Mitgliedsbeiträge", TaxSphere.NonProfit));

        category.Update(new AccountingCategory.CategoryData(AccountingCategoryKind.Income, null, "Beiträge", TaxSphere.NonProfit), isUsed: true);
        category.Number.Should().BeNull();

        FluentActions.Invoking(() => category.Update(new AccountingCategory.CategoryData(AccountingCategoryKind.Expense, null, "Beiträge", TaxSphere.NonProfit), isUsed: true))
            .Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingCategoryKindInUse));
    }

    [Fact]
    public void ArchivedData_CantBeUsedForNewEntries()
    {
        var costCenter = new CostCenter(7, " Jugend ");
        costCenter.SetArchived(true);

        costCenter.Name.Should().Be("Jugend");
        FluentActions.Invoking(costCenter.EnsureUsable).Should().Throw<PreconditionException>()
            .Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingArchived));
    }

    [Fact]
    public void Template_HasIncomeAndExpenseCategoriesInBothLanguages()
    {
        var german = AccountingTemplate.CreateCategories(7, german: true).ToList();
        var english = AccountingTemplate.CreateCategories(7, german: false).ToList();

        german.Should().Contain(c => c.Name == "Mitgliedsbeiträge" && c.Kind == AccountingCategoryKind.Income && c.TaxSphere == TaxSphere.NonProfit);
        german.Should().Contain(c => c.Kind == AccountingCategoryKind.Expense && c.TaxSphere == TaxSphere.AuxiliaryOperation);
        english.Select(c => c.Number).Should().Equal(german.Select(c => c.Number));
        german.Select(c => (c.Kind, c.Name)).Should().OnlyHaveUniqueItems();
        AccountingTemplate.CreateMoneyAccounts(7, german: true).Select(a => a.Kind).Should().Equal(MoneyAccountKind.Bank, MoneyAccountKind.Cash);
    }
}
