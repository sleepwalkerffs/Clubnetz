using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
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

    [Theory]
    [InlineData(MemberRole.Admin)]
    [InlineData(MemberRole.SportsDirector)]
    [InlineData(MemberRole.YouthSportsDirector)]
    [InlineData(MemberRole.Trainer)]
    public async Task Staff_CanBookForTwoStrangers(MemberRole role)
    {
        var seed = await SeedFamilies();
        var staffUserId = await AddStaff(role);

        var result = await Book(staffUserId, seed.StrangerMemberId, seed.OtherChildMemberId);

        result.Id.Should().BePositive();
        // Only the two players are validated and booked, the staff member is not one of them
        GetInstance<IBookingDomainService>().Received(1).BookCourt(
            Arg.Is<BookingDomainService.Context>(c => c.Participants.Select(p => p.Member.Id).Order().SequenceEqual(new[] { seed.StrangerMemberId, seed.OtherChildMemberId }.Order())),
            Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData(MemberRole.Maintainer)]
    [InlineData(MemberRole.Treasurer)]
    [InlineData(MemberRole.User)]
    public async Task OtherRoles_CannotBookForStrangers(MemberRole role)
    {
        var seed = await SeedFamilies();
        var userId = await AddStaff(role);

        var act = () => Book(userId, seed.StrangerMemberId, seed.OtherChildMemberId);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourt.ErrorCode.UserNotAllowedToBook));
    }

    [Fact]
    public async Task Staff_CanBookForStrangersInThePlanner()
    {
        var seed = await SeedFamilies();
        var staffUserId = await AddStaff(MemberRole.SportsDirector);
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        var result = await SendAsync(new BookCourts([new BookingSlotModel(TestDataSeed.Court1Id, interval.From, interval.To)], TimeZoneInfo.Utc.Id, playModeId,
            [seed.StrangerMemberId], Comment: null, DryRun: true, ClientTimeZoneOffset: null, staffUserId));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Staff_CanBookRecurringForStrangers_AndAFamilyMemberCannot()
    {
        var seed = await SeedFamilies();
        var staffUserId = await AddStaff(MemberRole.SportsDirector);
        var playModeId = await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];
            playMode.Update(playMode.AllowedRoles, playMode.Color, playMode.FixedPlayerCount,
                playMode.IsChargingBookingSubscription, playMode.Name, playMode.FixedDuration,
                playMode.CanOverbook, playMode.CommentAllowed, playMode.MaxBookingsPerSeason, allowRecurring: true);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            ctx.Add(new Season(ctx.TestData().Club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(60))));
            await ctx.SaveChangesAsync();
            return playMode.Id;
        });

        BookRecurringCourt Recurring(int userId, int player)
            => new(TestDataSeed.Court1Id, interval, TimeZoneInfo.Utc.Id, playModeId, [player], 1, null, null, null, userId);

        // The old check let anybody in some family book a series for strangers
        var act = () => SendAsync(Recurring(seed.ParentUserId, seed.StrangerMemberId));
        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookRecurringCourt.ErrorCode.UserNotAllowedToBook));

        var result = await SendAsync(Recurring(staffUserId, seed.StrangerMemberId));
        result.Id.Should().BePositive();
    }

    [Fact]
    public async Task ApplicationAdministratorWithoutMember_CanBookForStrangers_ButOneWithAMemberActsAsMember()
    {
        var seed = await SeedFamilies();
        var supportUserId = await QueryAsync(async ctx =>
        {
            var user = new User("support@test.com", "support@test.com", "Support", "Admin", new DateOnly(1980, 1, 1), Gender.Female);
            ctx.Add(user);
            await ctx.SaveChangesAsync();
            ctx.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = (int)UserRoles.Administrator });
            await ctx.SaveChangesAsync();
            return user.Id;
        });

        (await Book(supportUserId, seed.StrangerMemberId)).Id.Should().BePositive();

        // The test user is an application administrator and a plain member of the club: the member roles count
        var act = () => Book(seed.ParentUserId, seed.StrangerMemberId);
        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourt.ErrorCode.UserNotAllowedToBook));
    }

    private async Task<int> AddStaff(MemberRole role)
        => await QueryAsync(async ctx =>
        {
            var user = new User($"staff{role}@test.com", $"staff{role}@test.com", "Staff", role.ToString(), new DateOnly(1980, 1, 1), Gender.Female);
            ctx.Add(user);
            await ctx.SaveChangesAsync();
            ctx.Add(new ClubMember(user.Id, ctx.TestData().Club.Id, [MemberRole.User, role]));
            await ctx.SaveChangesAsync();
            return user.Id;
        });

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
