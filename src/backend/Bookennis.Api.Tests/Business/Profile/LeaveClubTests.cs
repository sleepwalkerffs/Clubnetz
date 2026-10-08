using System.Security.Claims;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using FluentAssertions;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class LeaveClubTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task LeaveClub_RemovesTheClubMembership_ButKeepsTheUserAndTheGuestMembership()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            var user = await MemberSeed.AddUserWithMember(ctx, "leaving@test.com");
            var guestMember = new GuestMember(user.UserId, TestDataSeed.ClubId);
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();
            return new { User = user, GuestMemberId = guestMember.Id };
        });
        SetCurrentUser(seeded.User.UserId);

        await SendAsync(new LeaveClub(TestDataSeed.ClubId));

        await QueryAsync(async ctx =>
        {
            (await ctx.ClubMembers.AnyAsync(m => m.Id == seeded.User.MemberId)).Should().BeFalse();
            (await ctx.GuestMembers.AnyAsync(m => m.Id == seeded.GuestMemberId)).Should().BeTrue();
            (await ctx.Users.AnyAsync(u => u.Id == seeded.User.UserId)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task LeaveClub_RemovesTheFamilyLink_AndKeepsTheFamilyOfTheOtherParent()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            var parent = await MemberSeed.AddUserWithMember(ctx, "parent@test.com");
            var otherParent = await MemberSeed.AddUserWithMember(ctx, "otherparent@test.com");
            var child = await MemberSeed.AddChild(ctx, parent.UserId, "Kid");
            return new
            {
                Parent = parent,
                OtherParent = otherParent,
                FamilyId = await MemberSeed.AddFamily(ctx, [parent.MemberId, otherParent.MemberId], [child.MemberId])
            };
        });
        SetCurrentUser(seeded.Parent.UserId);

        await SendAsync(new LeaveClub(TestDataSeed.ClubId));

        await QueryAsync(async ctx =>
        {
            var family = await ctx.Families.SingleAsync(f => f.Id == seeded.FamilyId);
            family.Parents.Select(p => p.MemberId).Should().BeEquivalentTo([seeded.OtherParent.MemberId]);
            (await ctx.FamilyMembers.AnyAsync(f => f.MemberId == seeded.Parent.MemberId)).Should().BeFalse();
        });
    }

    [Fact]
    public async Task LeaveClub_OnlyParent_DeletesTheFamily_ButKeepsTheChildMembership()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            var parent = await MemberSeed.AddUserWithMember(ctx, "parent@test.com");
            var child = await MemberSeed.AddChild(ctx, parent.UserId, "Kid");
            return new { Parent = parent, Child = child, FamilyId = await MemberSeed.AddFamily(ctx, [parent.MemberId], [child.MemberId]) };
        });
        SetCurrentUser(seeded.Parent.UserId);

        await SendAsync(new LeaveClub(TestDataSeed.ClubId));

        await QueryAsync(async ctx =>
        {
            (await ctx.Families.AnyAsync(f => f.Id == seeded.FamilyId)).Should().BeFalse();
            (await ctx.ClubMembers.AnyAsync(m => m.Id == seeded.Child.MemberId)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task LeaveClub_CancelsUpcomingBookingsWithoutOtherPlayers()
    {
        var now = DateTimeOffset.UtcNow;
        var seeded = await QueryAsync(async ctx =>
        {
            var user = await MemberSeed.AddUserWithMember(ctx, "leaving@test.com");
            return new
            {
                User = user,
                UpcomingAlone = await MemberSeed.AddBooking(ctx, now.AddDays(2), user.MemberId),
                UpcomingShared = await MemberSeed.AddBooking(ctx, now.AddDays(3), user.MemberId, TestDataSeed.Member1Id)
            };
        });
        SetCurrentUser(seeded.User.UserId);

        await SendAsync(new LeaveClub(TestDataSeed.ClubId));

        await QueryAsync(async ctx =>
        {
            (await ctx.Bookings.AnyAsync(b => b.Id == seeded.UpcomingAlone)).Should().BeFalse();
            var shared = await ctx.Bookings.Include(b => b.Players).SingleAsync(b => b.Id == seeded.UpcomingShared);
            shared.Players.Select(p => p.MemberId).Should().BeEquivalentTo([TestDataSeed.Member1Id]);
        });
    }

    [Fact]
    public async Task LeaveClub_LastAdmin_Throws()
    {
        SetCurrentUser(TestDataSeed.AdminId);

        var act = () => SendAsync(new LeaveClub(TestDataSeed.ClubId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(LeaveClub.ErrorCode.LastClubAdmin));
        (await QueryAsync(ctx => ctx.ClubMembers.AnyAsync(m => m.Id == TestDataSeed.Member2Id))).Should().BeTrue();
    }

    [Fact]
    public async Task LeaveClub_NotAMember_IsNoOp()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var user = new Domain.User.User("nomember@test.com", "nomember@test.com", "No", "Member", new DateOnly(1990, 1, 1), Domain.User.Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();
            return user.Id;
        });
        SetCurrentUser(userId);

        await SendAsync(new LeaveClub(TestDataSeed.ClubId));

        (await QueryAsync(ctx => ctx.ClubMembers.CountAsync())).Should().Be(2);
    }

    private void SetCurrentUser(int userId)
    {
        var userAccessor = GetInstance<IUserAccessor>();
        userAccessor.TryGetUser(user: out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });
    }
}
