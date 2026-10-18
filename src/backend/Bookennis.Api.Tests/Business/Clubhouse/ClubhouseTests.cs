using Bookennis.Api.Business.Clubhouse;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Clubhouse;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ClubhouseItemCategory = Bookennis.Shared.Controller.Clubhouse.ClubhouseItemCategory;
using ClubhousePaymentMethod = Bookennis.Domain.Clubhouse.ClubhousePaymentMethod;
using SharedPaymentMethod = Bookennis.Shared.Controller.Clubhouse.ClubhousePaymentMethod;
using SharedStatus = Bookennis.Shared.Controller.Clubhouse.ClubhouseTabStatus;

namespace Bookennis.Api.Tests.Business.Clubhouse;

/// <summary>The clubhouse handlers: the menu, the host's start screen, booking onto tabs, paying and the tabs of a member.</summary>
public class ClubhouseTests(TestFixture fixture) : TestBase(fixture)
{
    private const int HostUserId = TestDataSeed.AdminId;

    private Task<ClubhouseItemDto> AddItem(string name = "Beer", int priceCents = 450, ClubhouseItemCategory category = ClubhouseItemCategory.Drink, bool isAvailable = true, int sortOrder = 0)
        => SendAsync(new CreateClubhouseItem(TestDataSeed.ClubId, new SaveClubhouseItemModel { Name = name, PriceCents = priceCents, Category = category, IsAvailable = isAvailable, SortOrder = sortOrder }));

    private Task<ClubhouseTabDto> Open(int? memberId, string? guestName = null)
        => SendAsync(new OpenClubhouseTab(TestDataSeed.ClubId, HostUserId, memberId, guestName));

    private Task<ClubhouseTabDto> Book(int tabId, params (int ItemId, int Quantity)[] items)
        => SendAsync(new AddClubhouseTabItems(TestDataSeed.ClubId, HostUserId, tabId, items.Select(i => new ClubhouseTabItemModel { ItemId = i.ItemId, Quantity = i.Quantity }).ToList()));

    private Task<ClubhouseTabDto> Pay(int tabId, ClubhousePaymentMethod method = ClubhousePaymentMethod.Cash)
        => SendAsync(new PayClubhouseTab(TestDataSeed.ClubId, HostUserId, tabId, method));

    private static async Task<T> Rejected<T>(Func<Task<T>> act, ClubhouseErrorCode code)
    {
        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(code.ToString());
        return default!;
    }

    // ---------- menu ----------

    [Fact]
    public async Task Items_AreCreatedChangedListedInMenuOrderAndDeleted()
    {
        var toast = await AddItem("Toast", 550, ClubhouseItemCategory.Food);
        var water = await AddItem("Water", 250, sortOrder: 2);
        var beer = await AddItem("Beer", 450, sortOrder: 1);

        await SendAsync(new UpdateClubhouseItem(TestDataSeed.ClubId, water.Id, new SaveClubhouseItemModel { Name = "Sparkling water", PriceCents = 280, Category = ClubhouseItemCategory.Drink, IsAvailable = false, SortOrder = 2 }));
        var items = await SendAsync(new GetClubhouseItems(TestDataSeed.ClubId));

        items.Select(i => i.Name).Should().Equal("Beer", "Sparkling water", "Toast");
        items.Single(i => i.Id == water.Id).Should().BeEquivalentTo(new { PriceCents = 280, IsAvailable = false, Currency = "eur" }, o => o.ExcludingMissingMembers());

        await SendAsync(new DeleteClubhouseItem(TestDataSeed.ClubId, toast.Id));
        (await SendAsync(new GetClubhouseItems(TestDataSeed.ClubId))).Select(i => i.Id).Should().Equal(beer.Id, water.Id);
    }

    [Fact]
    public async Task Item_OfAnotherClub_NotFound()
    {
        var beer = await AddItem();

        await FluentActions.Awaiting(() => SendAsync(new UpdateClubhouseItem(TestDataSeed.ClubId + 1, beer.Id, new SaveClubhouseItemModel { Name = "X" })))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => SendAsync(new DeleteClubhouseItem(TestDataSeed.ClubId + 1, beer.Id)))
            .Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task DeletedItem_StaysOnTheTabWithNameAndPrice()
    {
        var beer = await AddItem("Beer", 450);
        var tab = await Open(TestDataSeed.Member1Id);
        await Book(tab.Id, (beer.Id, 2));

        await SendAsync(new DeleteClubhouseItem(TestDataSeed.ClubId, beer.Id));
        var reloaded = await SendAsync(new GetClubhouseTab(TestDataSeed.ClubId, tab.Id));

        reloaded.Lines.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ItemId = (int?)null, Name = "Beer", UnitPriceCents = 450, Quantity = 2, TotalCents = 900 }, o => o.ExcludingMissingMembers());
        reloaded.TotalCents.Should().Be(900);
    }

    // ---------- opening and booking ----------

    [Fact]
    public async Task Open_ForAMember_ReturnsTheSameOpenTab_UntilItIsPaid()
    {
        var beer = await AddItem();

        var first = await Open(TestDataSeed.Member1Id);
        var again = await Open(TestDataSeed.Member1Id);
        await Book(first.Id, (beer.Id, 1));
        await Pay(first.Id);
        var next = await Open(TestDataSeed.Member1Id);

        again.Id.Should().Be(first.Id);
        next.Id.Should().NotBe(first.Id);
        first.Should().BeEquivalentTo(new { MemberId = (int?)TestDataSeed.Member1Id, Name = "us er", IsWalkIn = false, Status = SharedStatus.Open, TotalCents = 0 }, o => o.ExcludingMissingMembers());
        (await QueryAsync(ctx => ctx.ClubhouseTabs.SingleAsync(t => t.Id == first.Id))).OpenedByMemberId.Should().Be(TestDataSeed.Member2Id);
    }

    [Fact]
    public async Task Open_ForAGuestFromOutside_AlwaysANewTab()
    {
        var first = await Open(null, "Table 3");
        var second = await Open(null, "Table 3");

        second.Id.Should().NotBe(first.Id);
        first.Should().BeEquivalentTo(new { MemberId = (int?)null, Name = "Table 3", IsWalkIn = true }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Open_MemberOfAnotherClubOrNobody_IsRejected()
    {
        await FluentActions.Awaiting(() => Open(987654)).Should().ThrowAsync<EntityNotFoundException>();
        await Rejected(() => Open(null, " "), ClubhouseErrorCode.ClubhouseTabGuestInvalid);
    }

    [Fact]
    public async Task Book_ARound_MergesTheSameItem_AndKeepsThePriceOfThatMoment()
    {
        var beer = await AddItem("Beer", 450);
        var toast = await AddItem("Toast", 550, ClubhouseItemCategory.Food);
        var tab = await Open(TestDataSeed.Member1Id);

        await Book(tab.Id, (beer.Id, 1), (toast.Id, 1), (beer.Id, 1));
        await SendAsync(new UpdateClubhouseItem(TestDataSeed.ClubId, beer.Id, new SaveClubhouseItemModel { Name = "Beer", PriceCents = 500, Category = ClubhouseItemCategory.Drink }));
        var booked = await Book(tab.Id, (beer.Id, 1));

        booked.Lines.Select(l => (l.Name, l.UnitPriceCents, l.Quantity)).Should().BeEquivalentTo([("Beer", 450, 2), ("Toast", 550, 1), ("Beer", 500, 1)]);
        booked.TotalCents.Should().Be(900 + 550 + 500);
        (await QueryAsync(ctx => ctx.ClubhouseTabLines.Where(l => l.ClubhouseTabId == tab.Id).Select(l => l.AddedByMemberId).Distinct().ToListAsync())).Should().Equal(TestDataSeed.Member2Id);
    }

    [Fact]
    public async Task Book_ItemOfAnotherClubOrHiddenOrNothing_IsRejected()
    {
        var hidden = await AddItem("Punch", 300, isAvailable: false);
        var foreignItem = await QueryAsync(async ctx =>
        {
            var club = new Domain.Clubs.Club("Other", new Global.Intervals.TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)), new Global.Intervals.TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)), []);
            ctx.Add(club);
            await ctx.SaveChangesAsync();
            var item = new ClubhouseItem(club.Id, new ClubhouseItem.ItemData("Foreign", Domain.Clubhouse.ClubhouseItemCategory.Drink, 100, true, 0));
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            return item.Id;
        });
        var tab = await Open(TestDataSeed.Member1Id);

        await FluentActions.Awaiting(() => Book(tab.Id, (foreignItem, 1))).Should().ThrowAsync<EntityNotFoundException>();
        await Rejected(() => Book(tab.Id, (hidden.Id, 1)), ClubhouseErrorCode.ClubhouseItemNotAvailable);
        await Rejected(() => Book(tab.Id), ClubhouseErrorCode.ClubhouseQuantityInvalid);
        (await QueryAsync(ctx => ctx.ClubhouseTabLines.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task TabOfAnotherClub_NotFound()
    {
        var tab = await Open(TestDataSeed.Member1Id);

        await FluentActions.Awaiting(() => SendAsync(new GetClubhouseTab(TestDataSeed.ClubId + 1, tab.Id))).Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => SendAsync(new PayClubhouseTab(TestDataSeed.ClubId + 1, HostUserId, tab.Id, ClubhousePaymentMethod.Cash))).Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task RemoveLine_TakesTheEntryOff()
    {
        var beer = await AddItem("Beer", 450);
        var toast = await AddItem("Toast", 550, ClubhouseItemCategory.Food);
        var tab = await Open(TestDataSeed.Member1Id);
        var booked = await Book(tab.Id, (beer.Id, 1), (toast.Id, 1));

        var removed = await SendAsync(new RemoveClubhouseTabLine(TestDataSeed.ClubId, tab.Id, booked.Lines.Single(l => l.Name == "Toast").Id));

        removed.Lines.Select(l => l.Name).Should().Equal("Beer");
        removed.TotalCents.Should().Be(450);
        (await QueryAsync(ctx => ctx.ClubhouseTabLines.CountAsync())).Should().Be(1);
        await FluentActions.Awaiting(() => SendAsync(new RemoveClubhouseTabLine(TestDataSeed.ClubId, tab.Id, 987654))).Should().ThrowAsync<EntityNotFoundException>();
    }

    // ---------- paying and discarding ----------

    [Fact]
    public async Task Pay_ClosesTheTab_ThenNothingCanBeBooked()
    {
        var beer = await AddItem();
        var tab = await Open(null, "Table 3");
        await Book(tab.Id, (beer.Id, 3));

        var paid = await Pay(tab.Id, ClubhousePaymentMethod.Card);

        paid.Should().BeEquivalentTo(new { Status = SharedStatus.Paid, PaymentMethod = (SharedPaymentMethod?)SharedPaymentMethod.Card, TotalCents = 1350 }, o => o.ExcludingMissingMembers());
        paid.PaidAt.Should().NotBeNull();
        await Rejected(() => Book(tab.Id, (beer.Id, 1)), ClubhouseErrorCode.ClubhouseTabNotOpen);
        await Rejected(() => Pay(tab.Id), ClubhouseErrorCode.ClubhouseTabNotOpen);
    }

    [Fact]
    public async Task Discard_OnlyAnEmptyOpenTab()
    {
        var beer = await AddItem();
        var empty = await Open(null, "Wrong guest");
        var full = await Open(TestDataSeed.Member1Id);
        await Book(full.Id, (beer.Id, 1));

        await SendAsync(new DiscardClubhouseTab(TestDataSeed.ClubId, empty.Id));
        await FluentActions.Awaiting(() => SendAsync(new DiscardClubhouseTab(TestDataSeed.ClubId, full.Id)))
            .Should().ThrowAsync<PreconditionException>().Where(e => e.ErrorCode == nameof(ClubhouseErrorCode.ClubhouseTabNotEmpty));

        (await QueryAsync(ctx => ctx.ClubhouseTabs.Select(t => t.Id).ToListAsync())).Should().Equal(full.Id);
    }

    [Fact]
    public async Task PaidTabs_AreListedWithTheSumsPerPaymentMethod()
    {
        var beer = await AddItem("Beer", 450);
        var cash = await Open(TestDataSeed.Member1Id);
        var card = await Open(null, "Table 3");
        var open = await Open(TestDataSeed.Member2Id);
        await Book(cash.Id, (beer.Id, 2));
        await Book(card.Id, (beer.Id, 1));
        await Book(open.Id, (beer.Id, 5));
        await Pay(cash.Id);
        await Pay(card.Id, ClubhousePaymentMethod.Card);

        var result = await SendAsync(new GetClubhouseTabs(TestDataSeed.ClubId, null, null, DateTimeOffset.UtcNow));
        var none = await SendAsync(new GetClubhouseTabs(TestDataSeed.ClubId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), null, DateTimeOffset.UtcNow));

        result.Tabs.Select(t => t.Id).Should().BeEquivalentTo([cash.Id, card.Id]);
        result.Should().BeEquivalentTo(new { CashCents = 900, CardCents = 450, OtherCents = 0 }, o => o.ExcludingMissingMembers());
        none.Tabs.Should().BeEmpty();
    }

    // ---------- the host's start screen ----------

    [Fact]
    public async Task Board_ListsOpenTabsWithTotals_AndWhatWasSoldToday()
    {
        var beer = await AddItem("Beer", 450);
        var walkIn = await Open(null, "Table 3");
        var member = await Open(TestDataSeed.Member1Id);
        var paid = await Open(TestDataSeed.Member2Id);
        await Book(walkIn.Id, (beer.Id, 1));
        await Book(member.Id, (beer.Id, 2));
        await Book(paid.Id, (beer.Id, 4));
        await Pay(paid.Id, ClubhousePaymentMethod.Other);

        var board = await SendAsync(new GetClubhouseBoard(TestDataSeed.ClubId, DateTimeOffset.UtcNow));

        // The tab booked onto last comes first
        board.OpenTabs.Select(t => (t.Id, t.Name, t.IsWalkIn, t.TotalCents, t.ItemCount)).Should().Equal((member.Id, "us er", false, 900, 2), (walkIn.Id, "Table 3", true, 450, 1));
        board.Today.Should().BeEquivalentTo(new ClubhouseDaySummaryDto { PaidTabCount = 1, CashCents = 0, CardCents = 0, OtherCents = 1800, OpenCents = 1350 });
    }

    [Fact]
    public async Task Board_PlayingToday_ClosestToNowFirst_WithoutMembersOfOtherClubsOrWithAnOpenTab()
    {
        // A fixed day far from the real date, so the regulars and today's sales of other tests don't matter
        var now = new DateTimeOffset(2030, 6, 12, 15, 0, 0, TimeSpan.Zero);
        var (soon, onCourt, earlier, yesterday, withTab) = await QueryAsync(async ctx =>
        {
            var a = await MemberSeed.AddUserWithMember(ctx, "soon@clubhouse.test");
            var b = await MemberSeed.AddUserWithMember(ctx, "oncourt@clubhouse.test");
            var c = await MemberSeed.AddUserWithMember(ctx, "earlier@clubhouse.test");
            var d = await MemberSeed.AddUserWithMember(ctx, "yesterday@clubhouse.test");
            var e = await MemberSeed.AddUserWithMember(ctx, "withtab@clubhouse.test");
            await MemberSeed.AddBooking(ctx, now.AddHours(1), a.MemberId);
            await MemberSeed.AddBooking(ctx, now.AddMinutes(-30), b.MemberId, e.MemberId);
            await MemberSeed.AddBooking(ctx, now.AddHours(-4), c.MemberId);
            await MemberSeed.AddBooking(ctx, now.AddDays(-1), d.MemberId);
            return (a.MemberId, b.MemberId, c.MemberId, d.MemberId, e.MemberId);
        });
        await Open(withTab);

        var board = await SendAsync(new GetClubhouseBoard(TestDataSeed.ClubId, now));

        board.PlayingToday.Select(g => g.MemberId).Should().Equal(onCourt, soon, earlier);
        board.PlayingToday.Should().NotContain(g => g.MemberId == yesterday || g.MemberId == withTab);
        board.PlayingToday[0].Should().BeEquivalentTo(new { FirstName = "First", LastName = "oncourt" }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Board_Regulars_MostTabsFirst_WithoutMembersAlreadyListed()
    {
        var beer = await AddItem();
        var (often, once) = await QueryAsync(async ctx =>
            ((await MemberSeed.AddUserWithMember(ctx, "often@clubhouse.test")).MemberId, (await MemberSeed.AddUserWithMember(ctx, "once@clubhouse.test")).MemberId));

        foreach (var memberId in new[] { often, often, often, once })
        {
            var tab = await Open(memberId);
            await Book(tab.Id, (beer.Id, 1));
            await Pay(tab.Id);
        }

        // An open tab: listed with the tabs, not as a regular
        await Open(TestDataSeed.Member1Id);
        var board = await SendAsync(new GetClubhouseBoard(TestDataSeed.ClubId, DateTimeOffset.UtcNow));

        board.Regulars.Select(g => g.MemberId).Should().Equal(often, once);
    }

    [Fact]
    public async Task Guests_AreAllMembersOfTheClubByLastName_NamesOnly()
    {
        var guests = await SendAsync(new GetClubhouseGuests(TestDataSeed.ClubId));

        guests.Select(g => (g.MemberId, g.LastName)).Should().Equal((TestDataSeed.Member1Id, "er"), (TestDataSeed.Member2Id, "min"));
    }

    // ---------- the member's own tabs ----------

    [Fact]
    public async Task MyTabs_OwnAndOfTheChildren_NotOfOthers()
    {
        var beer = await AddItem();
        var (child, other) = await QueryAsync(async ctx =>
        {
            var (_, childMemberId) = await MemberSeed.AddChild(ctx, TestDataSeed.UserId, "Kid");
            await MemberSeed.AddFamily(ctx, [TestDataSeed.Member1Id], [childMemberId]);
            var (_, otherMemberId) = await MemberSeed.AddUserWithMember(ctx, "other@clubhouse.test");
            return (childMemberId, otherMemberId);
        });

        var own = await Open(TestDataSeed.Member1Id);
        var childTab = await Open(child);
        var otherTab = await Open(other);
        await Book(own.Id, (beer.Id, 1));
        await Book(childTab.Id, (beer.Id, 1));
        await Book(otherTab.Id, (beer.Id, 1));
        await Pay(own.Id);

        var mine = await SendAsync(new GetMyClubhouseTabs(TestDataSeed.ClubId, TestDataSeed.UserId));

        mine.Open.Select(t => t.Id).Should().Equal(childTab.Id);
        mine.RecentlyPaid.Select(t => t.Id).Should().Equal(own.Id);
        mine.Open[0].Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task MyTabs_UserWithoutMember_IsEmpty()
    {
        var mine = await SendAsync(new GetMyClubhouseTabs(TestDataSeed.ClubId, 987654));

        mine.Open.Should().BeEmpty();
        mine.RecentlyPaid.Should().BeEmpty();
    }

    [Fact]
    public async Task MemberWhoLeaves_TabStaysWithoutTheName()
    {
        var beer = await AddItem();
        var (userId, memberId) = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "leaver@clubhouse.test"));
        var tab = await Open(memberId);
        await Book(tab.Id, (beer.Id, 1));

        await QueryAsync(async ctx =>
        {
            ctx.Remove(await ctx.ClubMembers.SingleAsync(m => m.Id == memberId));
            await ctx.SaveChangesAsync();
        });
        var board = await SendAsync(new GetClubhouseBoard(TestDataSeed.ClubId, DateTimeOffset.UtcNow));

        board.OpenTabs.Should().ContainSingle(t => t.Id == tab.Id).Which.Should().BeEquivalentTo(new { MemberId = (int?)null, Name = "", TotalCents = 450 }, o => o.ExcludingMissingMembers());
        userId.Should().BePositive();
    }

    [Fact]
    public void ClubhouseHost_IsNoStaffRole()
        => ActiveMembership.IsStaff([MemberRole.User, MemberRole.ClubhouseHost]).Should().BeFalse();
}
