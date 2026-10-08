using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class DeleteAdminUserTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteAdminUser_RemovesUserAndMemberships()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var user = new User("deleteuser@test.com", "deleteuser@test.com", "Delete", "User", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var club = ctx.TestData().Club;
            ctx.Add(new Domain.Members.ClubMember(user.Id, club.Id, [Domain.Members.MemberRole.User]));
            await ctx.SaveChangesAsync();

            return user.Id;
        });

        SetTenantId(0);

        await SendAsync(new DeleteAdminUser(userId));

        var membershipExists = await QueryAsync(ctx => ctx.ClubMembers.AnyAsync(m => m.UserId == userId));
        membershipExists.Should().BeFalse();
        var userExists = await QueryAsync(ctx => ctx.Users.AnyAsync(u => u.Id == userId));
        userExists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAdminUser_HandsSharedChildrenToTheOtherParent_AndDeletesChildrenWithoutOtherParent()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            var parent = await MemberSeed.AddUserWithMember(ctx, "parent@test.com");
            var otherParent = await MemberSeed.AddUserWithMember(ctx, "otherparent@test.com");
            var sharedChild = await MemberSeed.AddChild(ctx, parent.UserId, "Shared");
            var childWithoutFamily = await MemberSeed.AddChild(ctx, parent.UserId, "Alone");
            var familyId = await MemberSeed.AddFamily(ctx, [parent.MemberId, otherParent.MemberId], [sharedChild.MemberId]);

            return new { Parent = parent, OtherParent = otherParent, SharedChild = sharedChild, ChildWithoutFamily = childWithoutFamily, FamilyId = familyId };
        });

        await SendAsync(new DeleteAdminUser(seeded.Parent.UserId));

        await QueryAsync(async ctx =>
        {
            var sharedChild = await ctx.Users.SingleAsync(u => u.Id == seeded.SharedChild.UserId);
            sharedChild.BelongsToUserId.Should().Be(seeded.OtherParent.UserId);
            (await ctx.ClubMembers.AnyAsync(m => m.Id == seeded.SharedChild.MemberId)).Should().BeTrue();

            (await ctx.Users.AnyAsync(u => u.Id == seeded.ChildWithoutFamily.UserId)).Should().BeFalse();

            var family = await ctx.Families.SingleAsync(f => f.Id == seeded.FamilyId);
            family.Parents.Select(p => p.MemberId).Should().BeEquivalentTo([seeded.OtherParent.MemberId]);
            family.Children.Select(c => c.MemberId).Should().BeEquivalentTo([seeded.SharedChild.MemberId]);
            (await ctx.FamilyMembers.AnyAsync(f => f.MemberId == seeded.Parent.MemberId)).Should().BeFalse();
        });
    }

    [Fact]
    public async Task DeleteAdminUser_OnlyParent_DeletesTheFamilyAndTheChildren()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            var parent = await MemberSeed.AddUserWithMember(ctx, "parent@test.com");
            var child = await MemberSeed.AddChild(ctx, parent.UserId, "Kid");
            var familyId = await MemberSeed.AddFamily(ctx, [parent.MemberId], [child.MemberId]);
            return new { Parent = parent, Child = child, FamilyId = familyId };
        });

        await SendAsync(new DeleteAdminUser(seeded.Parent.UserId));

        await QueryAsync(async ctx =>
        {
            (await ctx.Families.AnyAsync(f => f.Id == seeded.FamilyId)).Should().BeFalse();
            (await ctx.FamilyMembers.AnyAsync(f => f.FamilyId == seeded.FamilyId)).Should().BeFalse();
            (await ctx.Users.AnyAsync(u => u.Id == seeded.Child.UserId)).Should().BeFalse();
            (await ctx.ClubMembers.AnyAsync(m => m.Id == seeded.Child.MemberId)).Should().BeFalse();
        });
    }

    [Fact]
    public async Task DeleteAdminUser_CancelsUpcomingBookingsWithoutOtherPlayers_AndKeepsTheOthers()
    {
        var now = DateTimeOffset.UtcNow;
        var seeded = await QueryAsync(async ctx =>
        {
            var user = await MemberSeed.AddUserWithMember(ctx, "leaving@test.com");
            return new
            {
                User = user,
                UpcomingAlone = await MemberSeed.AddBooking(ctx, now.AddDays(2), user.MemberId),
                UpcomingShared = await MemberSeed.AddBooking(ctx, now.AddDays(3), user.MemberId, TestDataSeed.Member1Id),
                PastAlone = await MemberSeed.AddBooking(ctx, now.AddDays(-3), user.MemberId)
            };
        });

        await SendAsync(new DeleteAdminUser(seeded.User.UserId));

        await QueryAsync(async ctx =>
        {
            (await ctx.Bookings.AnyAsync(b => b.Id == seeded.UpcomingAlone)).Should().BeFalse();
            (await ctx.Bookings.AnyAsync(b => b.Id == seeded.PastAlone)).Should().BeTrue();

            var shared = await ctx.Bookings.Include(b => b.Players).SingleAsync(b => b.Id == seeded.UpcomingShared);
            shared.Players.Select(p => p.MemberId).Should().BeEquivalentTo([TestDataSeed.Member1Id]);
        });
    }
}
