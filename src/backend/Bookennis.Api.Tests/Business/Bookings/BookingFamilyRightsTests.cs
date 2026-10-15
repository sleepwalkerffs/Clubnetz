using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

/// <summary>
/// A user may book for players when they play themself or when a player is a child of a family the user is a parent of.
/// Being in some other family gives no right to book for anybody.
/// </summary>
public class BookingFamilyRightsTests : TestBase
{
    private readonly DateTimeOffsetInterval interval;

    public BookingFamilyRightsTests(TestFixture fixture) : base(fixture)
    {
        var day = DateTime.UtcNow.Date.AddDays(3);
        interval = new DateTimeOffsetInterval(new DateTimeOffset(day.AddHours(10), TimeSpan.Zero), new DateTimeOffset(day.AddHours(11), TimeSpan.Zero));

        var bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();
        bookingService.BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(call => new Booking(TestDataSeed.ClubId, call.Arg<BookingDomainService.Context>().Court.Id, call.Arg<BookingDomainService.Context>().PlayMode.Id,
                call.Arg<DateTimeOffsetInterval>(), TimeZoneInfo.Utc.Id));
    }

    [Fact]
    public async Task ParentOfAnotherFamily_CannotBookForAStranger()
    {
        var seed = await SeedFamilies();

        var act = () => Book(seed.ParentUserId, seed.StrangerMemberId);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourt.ErrorCode.UserNotAllowedToBook));
    }

    [Fact]
    public async Task Parent_CannotBookForTheChildOfAnotherFamily()
    {
        var seed = await SeedFamilies();

        var act = () => Book(seed.ParentUserId, seed.OtherChildMemberId);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourt.ErrorCode.UserNotAllowedToBook));
    }

    [Fact]
    public async Task Child_CannotBookForItsParent()
    {
        var seed = await SeedFamilies();

        var act = () => Book(seed.ChildUserId, seed.ParentMemberId);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourt.ErrorCode.UserNotAllowedToBook));
    }

    [Fact]
    public async Task Parent_CanBookForTheOwnChild()
    {
        var seed = await SeedFamilies();

        var result = await Book(seed.ParentUserId, seed.ChildMemberId);

        result.Id.Should().BePositive();
    }

    [Fact]
    public async Task Player_CanBookWithAStranger()
    {
        var seed = await SeedFamilies();

        var result = await Book(seed.ParentUserId, seed.ParentMemberId, seed.StrangerMemberId);

        result.Id.Should().BePositive();
    }

    [Fact]
    public async Task Planner_ParentOfAnotherFamily_CannotBookForAStranger()
    {
        var seed = await SeedFamilies();
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        var act = () => SendAsync(new BookCourts([new BookingSlotModel(TestDataSeed.Court1Id, interval.From, interval.To)], TimeZoneInfo.Utc.Id, playModeId,
            [seed.StrangerMemberId], Comment: null, DryRun: true, ClientTimeZoneOffset: null, seed.ParentUserId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourt.ErrorCode.UserNotAllowedToBook));
    }

    private Task<BookCourtResult> Book(int userId, params int[] players)
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        return SendAsync(new BookCourt(TestDataSeed.Court1Id, interval, TimeZoneInfo.Utc.Id, playModeId, [.. players], null, null, userId));
    }

    private sealed record Seed(int ParentUserId, int ParentMemberId, int ChildUserId, int ChildMemberId, int OtherChildMemberId, int StrangerMemberId);

    /// <summary>
    /// The user of the test data (member 1) is the parent of a family with one child.
    /// Another family has its own parent and child, and a stranger is in no family.
    /// </summary>
    private Task<Seed> SeedFamilies() => QueryAsync(async ctx =>
    {
        var clubId = ctx.TestData().Club.Id;
        var parentMember = ctx.TestData().Member1;

        async Task<ClubMember> AddMember(string name, int? belongsToUserId = null)
        {
            var user = belongsToUserId is null
                ? new User($"{name}@test.com", $"{name}@test.com", name, "Test", new DateOnly(1980, 1, 1), Gender.Female)
                : new User(name, "Test", new DateOnly(2014, 1, 1), Gender.Male, belongsToUserId.Value);
            ctx.Add(user);
            await ctx.SaveChangesAsync();
            var member = new ClubMember(user.Id, clubId, [MemberRole.User]);
            ctx.Add(member);
            await ctx.SaveChangesAsync();
            return member;
        }

        var child = await AddMember("child", parentMember.UserId);
        var otherParent = await AddMember("otherparent");
        var otherChild = await AddMember("otherchild", otherParent.UserId);
        var stranger = await AddMember("stranger");

        ctx.Add(new Family(clubId, [parentMember.Id], [child.Id]));
        ctx.Add(new Family(clubId, [otherParent.Id], [otherChild.Id]));
        await ctx.SaveChangesAsync();

        return new Seed(parentMember.UserId, parentMember.Id, child.UserId, child.Id, otherChild.Id, stranger.Id);
    });
}
