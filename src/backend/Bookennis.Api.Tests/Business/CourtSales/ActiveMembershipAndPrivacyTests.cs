using System.Security.Claims;
using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary><see cref="ActiveMembership"/>, the anonymized calendar, the players of members who are not active, and bought bookings.</summary>
public class ActiveMembershipAndPrivacyTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ActiveMembership_StaffEnrolledAndParentsOfEnrolledChildren()
    {
        var result = await QueryAsync(async ctx =>
        {
            var seasonId = await CourtSaleSeed.AddActiveSeason(ctx);
            var (_, enrolled) = await MemberSeed.AddUserWithMember(ctx, "enrolled@test.com");
            await CourtSaleSeed.Enroll(ctx, enrolled, seasonId);
            var (_, trainer) = await MemberSeed.AddUserWithMember(ctx, "trainer@test.com", MemberRole.Trainer);
            var (parentUserId, parent) = await MemberSeed.AddUserWithMember(ctx, "parent@test.com");
            var (_, child) = await MemberSeed.AddChild(ctx, parentUserId, "Kid");
            await MemberSeed.AddFamily(ctx, [parent], [child]);
            await CourtSaleSeed.Enroll(ctx, child, seasonId);
            var (_, plain) = await MemberSeed.AddUserWithMember(ctx, "plain@test.com");

            return new
            {
                Enrolled = await ActiveMembership.IsActive(ctx, TestDataSeed.ClubId, enrolled, CancellationToken.None),
                Trainer = await ActiveMembership.IsActive(ctx, TestDataSeed.ClubId, trainer, CancellationToken.None),
                Parent = await ActiveMembership.IsActive(ctx, TestDataSeed.ClubId, parent, CancellationToken.None),
                ParentEnrolled = await ActiveMembership.IsEnrolledInActiveSeason(ctx, TestDataSeed.ClubId, parent, CancellationToken.None),
                Plain = await ActiveMembership.IsActive(ctx, TestDataSeed.ClubId, plain, CancellationToken.None),
                OtherClub = await ActiveMembership.IsActive(ctx, 987654, enrolled, CancellationToken.None),
            };
        });

        result.Should().BeEquivalentTo(new { Enrolled = true, Trainer = true, Parent = true, ParentEnrolled = false, Plain = false, OtherClub = false });
    }

    [Fact]
    public async Task GetBookings_ForAMemberWhoIsNotActive_OnlyTheOwnBookingsAreComplete()
    {
        var (viewerUserId, viewerMemberId, ownBookingId, otherBookingId) = await QueryAsync(async ctx =>
        {
            var (userId, memberId) = await MemberSeed.AddUserWithMember(ctx, "viewer@test.com");
            var from = DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(9);
            var own = await MemberSeed.AddBooking(ctx, new DateTimeOffset(from, TimeSpan.Zero), memberId);
            var other = await MemberSeed.AddBooking(ctx, new DateTimeOffset(from.AddHours(2), TimeSpan.Zero), TestDataSeed.Member1Id, TestDataSeed.Member2Id);
            var booking = await ctx.Bookings.SingleAsync(b => b.Id == other);
            booking.Update(booking.CourtId, booking.PlayModeId, booking.Interval, booking.TimeZoneInfoId, [TestDataSeed.Member1Id, TestDataSeed.Member2Id], "Secret comment");
            await ctx.SaveChangesAsync();
            return (userId, memberId, own, other);
        });

        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var anonymized = await SendAsync(new GetBookings(day, day, viewerUserId));
        var full = await SendAsync(new GetBookings(day, day));

        var own = anonymized.Bookings.Single(b => b.BookingEntryId == ownBookingId);
        own.IsAnonymized.Should().BeFalse();
        own.Players.Select(p => p.MemberId).Should().Equal(viewerMemberId);

        var other = anonymized.Bookings.Single(b => b.BookingEntryId == otherBookingId);
        other.Should().BeEquivalentTo(new { IsAnonymized = true, Comment = (string?)null, CourtId = TestDataSeed.Court1Id, RecurringBookingSeriesId = (int?)null }, o => o.ExcludingMissingMembers());
        other.Players.Should().BeEmpty();
        other.PlayMode.Should().BeEquivalentTo(BookingAnonymizer.NeutralPlayMode);

        full.Bookings.Single(b => b.BookingEntryId == otherBookingId).Comment.Should().Be("Secret comment");

        var single = await SendAsync(new GetBooking(otherBookingId, viewerUserId));
        single.IsAnonymized.Should().BeTrue();
        single.Players.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBookings_ParentWhoIsNotActive_SeesTheBookingsOfTheOwnChildren()
    {
        var (parentUserId, bookingId) = await QueryAsync(async ctx =>
        {
            var (userId, parent) = await MemberSeed.AddUserWithMember(ctx, "parent-calendar@test.com");
            var (_, child) = await MemberSeed.AddChild(ctx, userId, "Kid");
            await MemberSeed.AddFamily(ctx, [parent], [child]);
            var booking = await MemberSeed.AddBooking(ctx, new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(9), TimeSpan.Zero), child, TestDataSeed.Member1Id);
            return (userId, booking);
        });

        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var result = await SendAsync(new GetBookings(day, day, parentUserId));

        result.Bookings.Single(b => b.BookingEntryId == bookingId).IsAnonymized.Should().BeFalse();
    }

    [Fact]
    public async Task GetPlayers_OnlyTheGivenMembers_EvenWhenNotEnrolled()
    {
        var (userId, memberId) = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "solo@test.com"));
        var own = await SendAsync(new GetOwnAndChildMemberIds(TestDataSeed.ClubId, userId));

        var result = await SendAsync(new GetPlayers(own));

        result.ClubMembers.Select(p => p.MemberId).Should().Equal(memberId);
    }

    [Fact]
    public async Task GetPlayers_FormerGuestsAreNoPlayers()
    {
        await QueryAsync(async ctx =>
        {
            var user = new User("guest-player@test.com", "guest-player@test.com", "Guest", "Player", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();
            var guest = new GuestMember(user.Id, TestDataSeed.ClubId);
            guest.AllowBooking();
            ctx.Add(guest);
            await ctx.SaveChangesAsync();
            return 0;
        });

        (await SendAsync(new GetPlayers())).ClubMembers.Should().NotContain(p => p.FirstName == "Guest" && p.LastName == "Player");
    }

    [Fact]
    public async Task BoughtBooking_CannotBeDeletedOrEditedLikeOthers()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.EnableSales(ctx);
            var (_, buyer) = await CourtSaleSeed.AddBuyer(ctx);
            return (await CourtSaleSeed.AddPurchase(ctx, buyer, CourtSaleSeed.Tomorrow())).BookingId!.Value;
        });

        var delete = () => SendAsync(new DeleteBooking(bookingId));

        (await delete.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CourtPurchaseErrorCode.BookingIsBought));
        (await QueryAsync(ctx => ctx.Bookings.AnyAsync(b => b.Id == bookingId))).Should().BeTrue();
    }

    [Fact]
    public async Task BecomeClubMember_FormerGuest_GetsAClubMember()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var user = new User("former-guest-join@test.com", "former-guest-join@test.com", "Former", "Guest", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();
            ctx.Add(new GuestMember(user.Id, TestDataSeed.ClubId));
            await ctx.SaveChangesAsync();
            return user.Id;
        });
        GetInstance<IUserAccessor>().TryGetUser(out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });

        await SendAsync(new BecomeClubMember(TestDataSeed.ClubId));

        (await QueryAsync(ctx => ctx.ClubMembers.CountAsync(m => m.UserId == userId && m.ClubId == TestDataSeed.ClubId))).Should().Be(1);
    }

    [Fact]
    public async Task NotifyBoughtBookingsRemoved_HeldPurchase_IsReleasedWithoutMail()
    {
        var notifier = GetInstance<ICourtPurchaseNotifier>();
        notifier.ClearReceivedCalls();
        var purchaseId = await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.EnableSales(ctx);
            var (_, buyer) = await CourtSaleSeed.AddBuyer(ctx);
            var purchase = await CourtSaleSeed.AddPurchase(ctx, buyer, CourtSaleSeed.Tomorrow(), Domain.CourtSales.CourtPurchaseStatus.AwaitingPayment);
            ctx.Bookings.Remove(await ctx.Bookings.SingleAsync(b => b.Id == purchase.BookingId));
            await ctx.SaveChangesAsync();
            return purchase.Id;
        });

        await SendAsync(new NotifyBoughtBookingsRemoved([purchaseId]));

        var released = await QueryAsync(ctx => ctx.CourtPurchases.SingleAsync(p => p.Id == purchaseId));
        released.Status.Should().Be(Domain.CourtSales.CourtPurchaseStatus.Expired);
        released.BookingId.Should().BeNull();
        await notifier.DidNotReceiveWithAnyArgs().NeedsAttention(default!, default, default);
    }

    [Fact]
    public async Task NotifyBoughtBookingsRemoved_TellsTheTreasurers()
    {
        var notifier = GetInstance<ICourtPurchaseNotifier>();
        notifier.ClearReceivedCalls();
        var purchaseId = await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.EnableSales(ctx);
            var (_, buyer) = await CourtSaleSeed.AddBuyer(ctx);
            return (await CourtSaleSeed.AddPurchase(ctx, buyer, CourtSaleSeed.Tomorrow())).Id;
        });

        await SendAsync(new NotifyBoughtBookingsRemoved([purchaseId]));

        await notifier.Received(1).NeedsAttention(Arg.Is<Domain.CourtSales.CourtPurchase>(p => p.Id == purchaseId), CourtPurchaseAttentionReason.BookingRemoved, Arg.Any<CancellationToken>());
    }
}
