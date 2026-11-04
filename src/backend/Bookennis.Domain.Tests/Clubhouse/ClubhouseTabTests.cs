using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Clubhouse;

public class ClubhouseTabTests
{
    private const int ClubId = 7;
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 17, 0, 0, TimeSpan.Zero);

    private static ClubhouseItem Item(string name = "Beer", int priceCents = 450, bool isAvailable = true, int clubId = ClubId)
        => new(clubId, new ClubhouseItem.ItemData(name, ClubhouseItemCategory.Drink, priceCents, isAvailable, 0));

    private static ClubhouseTab MemberTab() => new(ClubId, memberId: 11, guestName: null, openedByMemberId: 99, Now);

    [Theory]
    [InlineData(null, null)]
    [InlineData(11, "Table 3")]
    [InlineData(null, "   ")]
    public void Tab_IsForAMemberOrForAGuestWithAName(int? memberId, string? guestName)
    {
        var act = () => new ClubhouseTab(ClubId, memberId, guestName, null, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseTabGuestInvalid));
    }

    [Fact]
    public void WalkInTab_KeepsTheTrimmedName()
    {
        var tab = new ClubhouseTab(ClubId, null, "  Table 3 ", null, Now);

        tab.GuestName.Should().Be("Table 3");
        tab.MemberId.Should().BeNull();
        tab.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Add_KeepsNameAndPriceOfThatMoment()
    {
        var beer = Item(priceCents: 450);
        var tab = MemberTab();

        tab.Add(beer, 2, 99, Now);
        beer.Update(new ClubhouseItem.ItemData("Craft beer", ClubhouseItemCategory.Drink, 600, true, 0));
        tab.Add(beer, 1, 99, Now.AddMinutes(30));

        tab.Lines.Select(l => (l.Name, l.UnitPriceCents, l.Quantity)).Should().Equal(("Beer", 450, 2), ("Craft beer", 600, 1));
        tab.TotalCents.Should().Be(2 * 450 + 600);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ClubhouseTab.MaxQuantity + 1)]
    public void Add_InvalidQuantity_Throws(int quantity)
    {
        var act = () => MemberTab().Add(Item(), quantity, null, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseQuantityInvalid));
    }

    [Fact]
    public void Add_HiddenItem_Throws()
    {
        var act = () => MemberTab().Add(Item(isAvailable: false), 1, null, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseItemNotAvailable));
    }

    [Fact]
    public void Add_ItemOfAnotherClub_Throws()
    {
        var act = () => MemberTab().Add(Item(clubId: ClubId + 1), 1, null, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPaid_ClosesTheTab_AndNothingChangesAfterwards()
    {
        var tab = MemberTab();
        tab.Add(Item(), 1, null, Now);

        tab.MarkPaid(ClubhousePaymentMethod.Card, 99, Now.AddHours(1));

        tab.Status.Should().Be(ClubhouseTabStatus.Paid);
        tab.PaymentMethod.Should().Be(ClubhousePaymentMethod.Card);
        tab.PaidAt.Should().Be(Now.AddHours(1));
        tab.PaidToMemberId.Should().Be(99);

        foreach (var act in new Action[] { () => tab.Add(Item(), 1, null, Now), () => tab.Remove(0), () => tab.MarkPaid(ClubhousePaymentMethod.Cash, null, Now), tab.EnsureCanBeDiscarded })
            act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseTabNotOpen));
    }

    [Fact]
    public void MarkPaid_EmptyTab_Throws()
    {
        var act = () => MemberTab().MarkPaid(ClubhousePaymentMethod.Cash, null, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseTabEmpty));
    }

    [Fact]
    public void Discard_OnlyAnEmptyTab()
    {
        var tab = MemberTab();
        tab.EnsureCanBeDiscarded();

        tab.Add(Item(), 1, null, Now);
        var act = tab.EnsureCanBeDiscarded;

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseTabNotEmpty));
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("Beer", -1)]
    [InlineData("Beer", ClubhouseItem.MaxPriceCents + 1)]
    public void Item_InvalidNameOrPrice_Throws(string name, int priceCents)
    {
        var act = () => Item(name, priceCents);

        act.Should().Throw<PreconditionException>();
    }

    [Fact]
    public void Item_IsTrimmedAndMayBeFree()
    {
        var water = Item("  Tap water ", 0);

        water.Name.Should().Be("Tap water");
        water.PriceCents.Should().Be(0);
        water.Currency.Should().Be("eur");
    }

    // ---------- paying online ----------

    [Fact]
    public void OnlinePayment_LocksTheTab_UntilItEnds()
    {
        var tab = MemberTab();
        tab.Add(Item(), 2, null, Now);

        tab.StartOnlinePayment();

        tab.Status.Should().Be(ClubhouseTabStatus.AwaitingPayment);
        tab.IsUnpaid.Should().BeTrue();
        foreach (var act in new Action[] { () => tab.Add(Item(), 1, null, Now), () => tab.Remove(1), () => tab.MarkPaid(ClubhousePaymentMethod.Cash, null, Now), tab.EnsureCanBeDiscarded, tab.StartOnlinePayment })
            act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseTabPaymentRunning));

        tab.EndOnlinePayment();

        tab.IsOpen.Should().BeTrue();
        tab.Add(Item(), 1, null, Now);
    }

    [Fact]
    public void OnlinePayment_NotForAnEmptyTabOrAGuestFromOutside()
    {
        var empty = MemberTab();
        var walkIn = new ClubhouseTab(ClubId, null, "Table 3", null, Now);
        walkIn.Add(Item(), 1, null, Now);

        empty.Invoking(t => t.StartOnlinePayment()).Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(ClubhouseErrorCode.ClubhouseTabEmpty));
        walkIn.Invoking(t => t.StartOnlinePayment()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPaidOnline_PaysTheTab_OrReportsThatItWasAlreadyPaid()
    {
        var tab = MemberTab();
        tab.Add(Item(), 2, null, Now);
        tab.StartOnlinePayment();

        tab.MarkPaidOnline(Now).Should().BeTrue();
        tab.Should().BeEquivalentTo(new { Status = ClubhouseTabStatus.Paid, PaymentMethod = (ClubhousePaymentMethod?)ClubhousePaymentMethod.Online, PaidAt = (DateTimeOffset?)Now, PaidToMemberId = (int?)null }, o => o.ExcludingMissingMembers());

        tab.MarkPaidOnline(Now.AddMinutes(5)).Should().BeFalse();
        tab.PaidAt.Should().Be(Now);
    }

    [Fact]
    public void Host_CannotMarkATabAsPaidOnline()
    {
        var tab = MemberTab();
        tab.Add(Item(), 1, null, Now);

        tab.Invoking(t => t.MarkPaid(ClubhousePaymentMethod.Online, 99, Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Payment_TakesTheAmountOfTheLockedTab_AndIsPaidOnce()
    {
        var tab = MemberTab();
        tab.Add(Item(priceCents: 450), 2, null, Now);
        tab.Invoking(t => new ClubhouseTabPayment(t, 11, "M1", Now)).Should().Throw<InvalidOperationException>();
        tab.StartOnlinePayment();

        var payment = new ClubhouseTabPayment(tab, 11, "M1", Now);

        payment.Should().BeEquivalentTo(new { AmountCents = 900, Currency = "eur", Status = ClubhouseTabPaymentStatus.Pending, ExpiresAt = Now + ClubhouseTabPayment.Lifetime }, o => o.ExcludingMissingMembers());
        payment.CheckoutReference.Length.Should().BeLessThanOrEqualTo(64);

        payment.MarkPaid("TX1", Now);
        payment.MarkPaid("TX2", Now.AddMinutes(1));
        payment.Expire();

        payment.Should().BeEquivalentTo(new { Status = ClubhouseTabPaymentStatus.Paid, TransactionCode = "TX1", PaidAt = (DateTimeOffset?)Now }, o => o.ExcludingMissingMembers());
    }
}
