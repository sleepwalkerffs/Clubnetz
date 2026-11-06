using Bookennis.Api.Business.PlayRequests;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.PlayRequests;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.PlayRequests;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.PlayRequests;

public class PlayRequestHandlerTests : TestBase
{
    private readonly IBookingDomainService bookingService;
    private readonly IPlayRequestPushNotifier notifier;

    public PlayRequestHandlerTests(TestFixture fixture) : base(fixture)
    {
        bookingService = PlayRequestSeed.BookingServiceBooksUnlessOccupied(GetInstance<IBookingDomainService>());
        notifier = GetInstance<IPlayRequestPushNotifier>();
        notifier.ClearSubstitute();
    }

    private int PlayModeId => Query(ctx => ctx.PlayModes.First(p => p.ClubId == TestDataSeed.ClubId).Id);

    private CreatePlayRequest Create(int userId, int[] invitees, int[]? courts = null, DateTimeOffset? expiresAt = null, int? playersNeeded = null, int? playModeId = null)
    {
        var start = PlayRequestSeed.Start;
        return new CreatePlayRequest(
            TestDataSeed.ClubId,
            userId,
            playModeId ?? PlayModeId,
            new DateTimeOffsetInterval(start, start.AddMinutes(90)),
            TimeZoneInfo.Utc.Id,
            [.. courts ?? [TestDataSeed.Court1Id, TestDataSeed.Court2Id]],
            playersNeeded,
            [.. invitees],
            "Anybody?",
            expiresAt);
    }

    // ---------- create ----------

    [Fact]
    public async Task CreatePlayRequest_SavesRequestAndInvitees_AndNotifiesThem()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));

        var id = await SendAsync(Create(requesterUser, [TestDataSeed.Member2Id]));

        var request = await QueryAsync(ctx => ctx.PlayRequests.Include(r => r.Invitees).SingleAsync(r => r.Id == id));
        request.RequesterMemberId.Should().Be(requester);
        request.Status.Should().Be(PlayRequestStatus.Open);
        request.PlayersNeeded.Should().Be(2, "the play mode of the test club has a fixed player count of 2");
        request.CourtIds.Should().Equal(TestDataSeed.Court1Id, TestDataSeed.Court2Id);
        request.Comment.Should().Be("Anybody?");
        request.Invitees.Select(i => i.MemberId).Should().Equal(TestDataSeed.Member2Id);
        await notifier.Received(1).RequestCreated(id, Arg.Any<CancellationToken>());
        // The request blocks nothing and books nothing
        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task CreatePlayRequest_ChecksTheBookingRulesForTheRequester_AndCreatesNothingWhenTheyAreBroken()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        bookingService.ClearSubstitute();
        bookingService
            .BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new PreconditionException(BookingDomainService.ErrorCode.MaximumReservationsReached, "max"));

        var act = () => SendAsync(Create(requesterUser, [TestDataSeed.Member2Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.MaximumReservationsReached));
        (await QueryAsync(ctx => ctx.PlayRequests.CountAsync())).Should().Be(0);
        await notifier.DidNotReceiveWithAnyArgs().RequestCreated(default, default);
    }

    [Fact]
    public async Task CreatePlayRequest_EveryCourtTaken_Throws()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        PlayRequestSeed.BookingServiceBooksUnlessOccupied(bookingService, TestDataSeed.Court1Id, TestDataSeed.Court2Id);

        var act = () => SendAsync(Create(requesterUser, [TestDataSeed.Member2Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));
        (await QueryAsync(ctx => ctx.PlayRequests.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task CreatePlayRequest_ValidatesAsIfTheRequesterWasAllPlayers()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));

        await SendAsync(Create(requesterUser, [TestDataSeed.Member2Id]));

        // Singles need 2 players: the rules see 2 players (the requester twice) so the player count rule does not fail the request
        bookingService.Received(1).BookCourt(
            Arg.Is<BookingDomainService.Context>(c => c.Participants.Count == 2 && c.Participants.All(p => p.Member.Id == requester)),
            Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task CreatePlayRequest_AsksAMemberWhoIsNotActive_Throws()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));

        // Member1 has the role User only and is not enrolled in a season
        var act = () => SendAsync(Create(requesterUser, [TestDataSeed.Member1Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestMemberNotActive));
    }

    [Fact]
    public async Task CreatePlayRequest_RequesterIsNotActive_Throws()
    {
        var act = () => SendAsync(Create(TestDataSeed.UserId, [TestDataSeed.Member2Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestMemberNotActive));
    }

    [Fact]
    public async Task CreatePlayRequest_PlayModeTheRequesterMayNotBook_Throws()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var restricted = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == TestDataSeed.ClubId);
            club.AddPlayMode(new Domain.Clubs.Club.PlayModeDto([Domain.Members.MemberRole.Maintainer], System.Drawing.Color.Red, 2, true, "Maintainers only", null, false, false));
            await ctx.SaveChangesAsync();
            return club.PlayModes.Single(p => p.Name == "Maintainers only").Id;
        });

        var act = () => SendAsync(Create(requesterUser, [TestDataSeed.Member2Id], playModeId: restricted));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestPlayModeNotAllowed));
    }

    [Fact]
    public async Task CreatePlayRequest_MemberOfTheClubDoesNotExist_Throws()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));

        var act = () => SendAsync(Create(requesterUser, [999_999]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestMemberNotActive));
    }

    [Fact]
    public async Task CreatePlayRequest_CourtOfAnotherClub_Throws()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));

        var act = () => SendAsync(Create(requesterUser, [TestDataSeed.Member2Id], courts: [987_654]));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task CreatePlayRequest_EarlierExpiry_IsSaved()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var expiresAt = PlayRequestSeed.Start.AddHours(-6);

        var id = await SendAsync(Create(requesterUser, [TestDataSeed.Member2Id], expiresAt: expiresAt));

        (await QueryAsync(ctx => ctx.PlayRequests.SingleAsync(r => r.Id == id))).ExpiresAt.Should().Be(expiresAt);
    }

    // ---------- accept ----------

    [Fact]
    public async Task AcceptPlayRequest_Singles_BooksTheFirstCourtForBothPlayers_AndFillsTheRequest()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        var result = await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        result.IsFilled.Should().BeTrue();
        var booking = await QueryAsync(ctx => ctx.Bookings.Include(b => b.Players).SingleAsync());
        result.BookingId.Should().Be(booking.Id);
        booking.CourtId.Should().Be(TestDataSeed.Court1Id);
        booking.Interval.From.Should().Be(request.StartsAt);
        booking.Interval.To.Should().Be(request.EndsAt);
        booking.Players.Select(p => p.MemberId).Should().BeEquivalentTo([requester, TestDataSeed.Member2Id]);
        var saved = await QueryAsync(ctx => ctx.PlayRequests.SingleAsync());
        saved.Status.Should().Be(PlayRequestStatus.Filled);
        saved.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public async Task AcceptPlayRequest_FirstCourtTaken_BooksTheNextOne()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));
        PlayRequestSeed.BookingServiceBooksUnlessOccupied(bookingService, TestDataSeed.Court1Id);

        await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        (await QueryAsync(ctx => ctx.Bookings.SingleAsync())).CourtId.Should().Be(TestDataSeed.Court2Id);
    }

    [Fact]
    public async Task AcceptPlayRequest_EveryCourtTaken_ThrowsAndLeavesTheRequestAsItWas()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));
        PlayRequestSeed.BookingServiceBooksUnlessOccupied(bookingService, TestDataSeed.Court1Id, TestDataSeed.Court2Id);

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));
        var saved = await QueryAsync(ctx => ctx.PlayRequests.Include(r => r.Invitees).SingleAsync());
        saved.Status.Should().Be(PlayRequestStatus.Open);
        saved.Invitees.Single().Status.Should().Be(PlayRequestInviteeStatus.Invited, "a failed booking saves nothing");
        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task AcceptPlayRequest_BrokenBookingRule_ThrowsTheRuleAndLeavesTheRequestOpen()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));
        bookingService.ClearSubstitute();
        bookingService
            .BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new PreconditionException(BookingDomainService.ErrorCode.MaxBookingsPerSeasonReached, ["Admin", "Single"], "quota"));

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.MaxBookingsPerSeasonReached));
        // Only a taken court is a reason to try the next one: a quota is the same on every court
        bookingService.ReceivedWithAnyArgs(1).BookCourt(default!, default, default!, default);
        (await QueryAsync(ctx => ctx.PlayRequests.SingleAsync())).Status.Should().Be(PlayRequestStatus.Open);
    }

    [Fact]
    public async Task AcceptPlayRequest_Doubles_BooksWhenTheLastSpotIsFilled()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (user1, player1) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var (user2, player2) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "carol"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [player1, player2, TestDataSeed.Member2Id], playersNeeded: 4));

        var first = await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, user1, request.Id));
        var second = await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, user2, request.Id));

        first.IsFilled.Should().BeFalse();
        second.IsFilled.Should().BeFalse();
        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(0, "doubles are only booked when every spot is filled");
        await notifier.Received(1).Accepted(request.Id, player1, Arg.Any<CancellationToken>());
        await notifier.Received(1).Accepted(request.Id, player2, Arg.Any<CancellationToken>());

        var third = await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        third.IsFilled.Should().BeTrue();
        var booking = await QueryAsync(ctx => ctx.Bookings.Include(b => b.Players).SingleAsync());
        booking.Players.Select(p => p.MemberId).Should().BeEquivalentTo([requester, player1, player2, TestDataSeed.Member2Id]);
        (await QueryAsync(ctx => ctx.PlayRequests.SingleAsync())).Status.Should().Be(PlayRequestStatus.Filled);
    }

    [Fact]
    public async Task AcceptPlayRequest_NotLastSpot_ChecksTheBookingRulesForTheAcceptorToo()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var (_, carol) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "carol"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [bob, carol, TestDataSeed.Member2Id], playersNeeded: 4));
        bookingService.ClearSubstitute();
        bookingService
            .BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new PreconditionException(BookingDomainService.ErrorCode.MemberNotAllowedInSeason, ["bob"], "not enrolled"));

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, bobUser, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.MemberNotAllowedInSeason));
        (await QueryAsync(ctx => ctx.PlayRequestInvitees.CountAsync(i => i.Status == PlayRequestInviteeStatus.Accepted))).Should().Be(0);
    }

    [Fact]
    public async Task CreatePlayRequest_ChildWithoutLogin_CannotBeAsked()
    {
        var (requesterUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (_, childMember) = await QueryAsync(async ctx =>
        {
            var child = await MemberSeed.AddChild(ctx, TestDataSeed.AdminId, "Kid");
            var member = await ctx.ClubMembers.SingleAsync(m => m.Id == child.MemberId);
            member.UpdateRoles([Domain.Members.MemberRole.User, Domain.Members.MemberRole.Trainer]);
            await ctx.SaveChangesAsync();
            return child;
        });

        var act = () => SendAsync(Create(requesterUser, [childMember]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestMemberNotActive));
    }

    [Fact]
    public async Task AcceptPlayRequest_ByAMemberWhoWasNotAsked_Throws()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (otherUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, otherUser, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotInvited));
        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task AcceptPlayRequest_ByTheRequester_Throws()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, requesterUser, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestOwnRequest));
    }

    [Fact]
    public async Task AcceptPlayRequest_Expired_Throws()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id], expiresAt: DateTimeOffset.UtcNow.AddMinutes(5)));
        await QueryAsync(ctx => ctx.Database.ExecuteSqlAsync($"""UPDATE "PlayRequests" SET "ExpiresAt" = NOW() - interval '1 minute' WHERE "Id" = {request.Id}"""));

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestExpired));
    }

    [Fact]
    public async Task AcceptPlayRequest_InviteeIsNoLongerActive_Throws()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        // The invitee lost their staff role after they were asked
        var (inviteeUser, invitee) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [invitee]));
        await QueryAsync(async ctx =>
        {
            var member = await ctx.ClubMembers.SingleAsync(m => m.Id == invitee);
            member.UpdateRoles([Domain.Members.MemberRole.User]);
            await ctx.SaveChangesAsync();
        });

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, inviteeUser, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestMemberNotActive));
    }

    [Fact]
    public async Task AcceptPlayRequest_RequestOfAnotherClub_IsNotFound()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId + 1, TestDataSeed.AdminId, request.Id));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    // ---------- decline / cancel ----------

    [Fact]
    public async Task DeclinePlayRequest_MarksTheInviteeAndKeepsTheRequestOpen()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (user, player) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [player, TestDataSeed.Member2Id]));

        await SendAsync(new DeclinePlayRequest(TestDataSeed.ClubId, user, request.Id));

        var saved = await QueryAsync(ctx => ctx.PlayRequests.Include(r => r.Invitees).SingleAsync());
        saved.Status.Should().Be(PlayRequestStatus.Open);
        saved.Invitees.Single(i => i.MemberId == player).Status.Should().Be(PlayRequestInviteeStatus.Declined);
        saved.Invitees.Single(i => i.MemberId == TestDataSeed.Member2Id).Status.Should().Be(PlayRequestInviteeStatus.Invited);
    }

    [Fact]
    public async Task DeclinePlayRequest_NotInvited_Throws()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new DeclinePlayRequest(TestDataSeed.ClubId, TestDataSeed.UserId, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotInvited));
    }

    [Fact]
    public async Task CancelPlayRequest_ByTheRequester_ClosesItAndNotifies()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        await SendAsync(new CancelPlayRequest(TestDataSeed.ClubId, requesterUser, request.Id));

        (await QueryAsync(ctx => ctx.PlayRequests.SingleAsync())).Status.Should().Be(PlayRequestStatus.Cancelled);
        await notifier.Received(1).Cancelled(request.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelPlayRequest_ByAnInvitee_Throws()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new CancelPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotRequester));
        await notifier.DidNotReceiveWithAnyArgs().Cancelled(default, default);
    }

    [Fact]
    public async Task CancelPlayRequest_AlreadyFilled_Throws()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));
        await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        var act = () => SendAsync(new CancelPlayRequest(TestDataSeed.ClubId, requesterUser, request.Id));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotOpen));
    }

    // ---------- reading ----------

    [Fact]
    public async Task GetPlayRequests_Requester_SeesEverybodyAndTheirAnswers()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var (_, carol) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "carol"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [bob, carol, TestDataSeed.Member2Id], playersNeeded: 4));
        await SendAsync(new DeclinePlayRequest(TestDataSeed.ClubId, bobUser, request.Id));
        await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        var result = await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, requesterUser));

        var dto = result.Should().ContainSingle().Subject;
        dto.IsMine.Should().BeTrue();
        dto.MyAnswer.Should().BeNull();
        dto.State.Should().Be(PlayRequestState.Open);
        dto.OpenSpots.Should().Be(2);
        dto.PlayModeName.Should().Be("Single");
        dto.Courts.Select(c => c.Name).Should().Equal("Court 1", "Court 2");
        dto.Invitees.Should().HaveCount(3);
        dto.Invitees.Single(i => i.MemberId == bob).Answer.Should().Be(PlayRequestAnswer.Declined);
        dto.Invitees.Single(i => i.MemberId == TestDataSeed.Member2Id).Answer.Should().Be(PlayRequestAnswer.Accepted);
        dto.Invitees.First().Answer.Should().Be(PlayRequestAnswer.Accepted, "those who accepted come first");
    }

    [Fact]
    public async Task GetPlayRequests_Invitee_OnlySeesWhoAcceptedNotWhoWasAskedOrDeclined()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var (carolUser, carol) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "carol"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [bob, carol, TestDataSeed.Member2Id], playersNeeded: 4));
        await SendAsync(new DeclinePlayRequest(TestDataSeed.ClubId, bobUser, request.Id));
        await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, request.Id));

        var result = await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, carolUser));

        var dto = result.Should().ContainSingle().Subject;
        dto.IsMine.Should().BeFalse();
        dto.MyAnswer.Should().Be(PlayRequestAnswer.Invited);
        dto.RequesterFirstName.Should().Be("alice");
        dto.Invitees.Should().ContainSingle().Which.MemberId.Should().Be(TestDataSeed.Member2Id);
    }

    [Fact]
    public async Task GetPlayRequests_MemberWhoWasNotAsked_SeesNothing()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (strangerUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "stranger"));
        await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        (await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, strangerUser))).Should().BeEmpty();
        (await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, TestDataSeed.AdminId))).Should().ContainSingle();
    }

    [Fact]
    public async Task GetPlayRequests_Invitee_DoesNotSeeCancelledExpiredOrOtherwiseFilledRequests()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (carolUser, carol) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "carol"));
        var cancelled = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [carol, TestDataSeed.Member2Id]));
        var expired = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [carol, TestDataSeed.Member2Id], expiresAt: DateTimeOffset.UtcNow.AddMinutes(5)));
        var filled = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [carol, TestDataSeed.Member2Id]));
        await SendAsync(new CancelPlayRequest(TestDataSeed.ClubId, requesterUser, cancelled.Id));
        await QueryAsync(ctx => ctx.Database.ExecuteSqlAsync($"""UPDATE "PlayRequests" SET "ExpiresAt" = NOW() - interval '1 minute' WHERE "Id" = {expired.Id}"""));
        await SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, TestDataSeed.AdminId, filled.Id));

        var forCarol = await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, carolUser));
        var forRequester = await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, requesterUser));
        var forAdmin = await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, TestDataSeed.AdminId));

        forCarol.Should().BeEmpty();
        forRequester.Select(r => r.State).Should().BeEquivalentTo([PlayRequestState.Cancelled, PlayRequestState.Expired, PlayRequestState.Filled]);
        forAdmin.Should().ContainSingle("the one who accepted keeps seeing the filled request").Which.State.Should().Be(PlayRequestState.Filled);
    }

    [Fact]
    public async Task GetPlayRequests_LeavesOutOldRequests()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));
        await QueryAsync(ctx => ctx.Database.ExecuteSqlAsync($"""UPDATE "PlayRequests" SET "StartsAt" = NOW() - interval '3 days' WHERE "Id" = {request.Id}"""));

        (await SendAsync(new GetPlayRequests(TestDataSeed.ClubId, requesterUser))).Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlayRequestCandidates_ActiveMembersOfTheClub_WithoutTheCaller()
    {
        var (callerUser, caller) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (_, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));

        var result = await SendAsync(new GetPlayRequestCandidates(TestDataSeed.ClubId, callerUser));

        // Member1 (role User, not enrolled) is not active
        result.Select(c => c.MemberId).Should().BeEquivalentTo([bob, TestDataSeed.Member2Id]);
        result.Should().NotContain(c => c.MemberId == caller);
    }

    [Fact]
    public async Task GetPlayRequestCandidates_IncludesMembersEnrolledInTheActiveSeason()
    {
        var (callerUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        await QueryAsync(async ctx =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Domain.Clubs.Season(TestDataSeed.ClubId, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            ctx.Add(new Domain.Members.MemberSeason(TestDataSeed.Member1Id, season.Id));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetPlayRequestCandidates(TestDataSeed.ClubId, callerUser));

        result.Select(c => c.MemberId).Should().Contain(TestDataSeed.Member1Id);
    }
}
