using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class AddChildTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddChild_AddsChildToExistingFamily()
    {
        var (familyId, newChildMemberId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], []);
            ctx.Add(family);
            await ctx.SaveChangesAsync();

            var childUser = new User("child@test.com", "child@test.com", "Child", "User", new DateOnly(2010, 1, 1), Gender.Male);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();
            var childMember = new ClubMember(childUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            return (family.Id, childMember.Id);
        });

        await SendAsync(new AddChild(familyId, [newChildMemberId]));

        var family = await QueryAsync(ctx => ctx.Families
            .Include(f => f.Children)
            .SingleAsync(f => f.Id == familyId));
        family.Children.Should().Contain(c => c.MemberId == newChildMemberId);
    }

    [Fact]
    public async Task AddChild_MemberWithOtherFamily_ThrowsArgumentException()
    {
        var (familyId, memberWithFamilyId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family1 = new Domain.Families.Family(club.Id, [member1.Id], []);
            ctx.Add(family1);
            await ctx.SaveChangesAsync();

            var childUser = new User("child2@test.com", "child2@test.com", "Child2", "User", new DateOnly(2010, 1, 1), Gender.Male);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();
            var childMember = new ClubMember(childUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            var family2 = new Domain.Families.Family(club.Id, [member2.Id], [childMember.Id]);
            ctx.Add(family2);
            await ctx.SaveChangesAsync();

            return (family1.Id, childMember.Id);
        });

        var act = () => SendAsync(new AddChild(familyId, [memberWithFamilyId]));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
