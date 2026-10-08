using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class AddParentTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddParent_AddsSecondParentToFamily()
    {
        var (familyId, secondParentMemberId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], []);
            ctx.Add(family);
            await ctx.SaveChangesAsync();

            var parentUser = new User("parent2@test.com", "parent2@test.com", "Parent2", "User", new DateOnly(1985, 1, 1), Gender.Female);
            ctx.Add(parentUser);
            await ctx.SaveChangesAsync();
            var parentMember = new ClubMember(parentUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(parentMember);
            await ctx.SaveChangesAsync();

            return (family.Id, parentMember.Id);
        });

        await SendAsync(new AddParent(familyId, secondParentMemberId));

        var family = await QueryAsync(ctx => ctx.Families
            .Include(f => f.Parents)
            .SingleAsync(f => f.Id == familyId));
        family.Parents.Should().HaveCount(2);
        family.Parents.Should().Contain(p => p.MemberId == secondParentMemberId);
    }

    [Fact]
    public async Task AddParent_MemberWithOtherFamily_ThrowsArgumentException()
    {
        var (familyId, memberWithFamilyId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family1 = new Domain.Families.Family(club.Id, [member1.Id], []);
            ctx.Add(family1);
            await ctx.SaveChangesAsync();

            var parentUser = new User("parent3@test.com", "parent3@test.com", "Parent3", "User", new DateOnly(1985, 1, 1), Gender.Male);
            ctx.Add(parentUser);
            await ctx.SaveChangesAsync();
            var parentMember = new ClubMember(parentUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(parentMember);
            await ctx.SaveChangesAsync();

            var family2 = new Domain.Families.Family(club.Id, [member2.Id, parentMember.Id], []);
            ctx.Add(family2);
            await ctx.SaveChangesAsync();

            return (family1.Id, parentMember.Id);
        });

        var act = () => SendAsync(new AddParent(familyId, memberWithFamilyId));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
