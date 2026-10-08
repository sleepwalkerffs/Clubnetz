using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Families;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class UpdateOwnedChildTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateOwnedChild_UpdatesOwnedChildDetails()
    {
        var (familyId, childMemberId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var club = ctx.TestData().Club;
            var parentUser = ctx.TestData().User;

            var childUser = new User("Child", "User", new DateOnly(2010, 1, 1), Gender.Male, parentUser.Id);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();

            var childMember = new ClubMember(childUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            var family = new Domain.Families.Family(club.Id, [member1.Id], [childMember.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();

            return (family.Id, childMember.Id);
        });

        var request = new UpdateOwnedChildRequest("UpdatedFirst", "UpdatedLast", new DateOnly(2012, 6, 15), Bookennis.Shared.Controller.Shared.Gender.Female);
        await SendAsync(new UpdateOwnedChild(familyId, childMemberId, request));

        var updatedUser = await QueryAsync(async ctx =>
        {
            var member = await ctx.ClubMembers.SingleAsync(m => m.Id == childMemberId);
            return await ctx.Users.SingleAsync(u => u.Id == member.UserId);
        });

        updatedUser.FirstName.Should().Be("UpdatedFirst");
        updatedUser.LastName.Should().Be("UpdatedLast");
        updatedUser.Birthday.Should().Be(new DateOnly(2012, 6, 15));
        updatedUser.Gender.Should().Be(Gender.Female);
    }

    [Fact]
    public async Task UpdateOwnedChild_NotInFamily_ThrowsArgumentException()
    {
        var (familyId, otherMemberId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], []);
            ctx.Add(family);
            await ctx.SaveChangesAsync();

            return (family.Id, member2.Id);
        });

        var request = new UpdateOwnedChildRequest("First", "Last", new DateOnly(2010, 1, 1), Bookennis.Shared.Controller.Shared.Gender.Male);
        var act = () => SendAsync(new UpdateOwnedChild(familyId, otherMemberId, request));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not a child*");
    }

    [Fact]
    public async Task UpdateOwnedChild_NotOwnedAccount_ThrowsArgumentException()
    {
        var (familyId, childMemberId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var club = ctx.TestData().Club;

            var childUser = new User("child@test.com", "child@test.com", "Child", "User", new DateOnly(2010, 1, 1), Gender.Male);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();

            var childMember = new ClubMember(childUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            var family = new Domain.Families.Family(club.Id, [member1.Id], [childMember.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();

            return (family.Id, childMember.Id);
        });

        var request = new UpdateOwnedChildRequest("First", "Last", new DateOnly(2010, 1, 1), Bookennis.Shared.Controller.Shared.Gender.Male);
        var act = () => SendAsync(new UpdateOwnedChild(familyId, childMemberId, request));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not an owned account*");
    }
}
