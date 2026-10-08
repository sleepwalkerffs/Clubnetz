using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class GetAvailableFamilyMembersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAvailableFamilyMembers_ExcludesRequestingMember()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        var result = await SendAsync(new GetAvailableFamilyMembers(memberId));

        result.AvailableMembers.Should().NotContain(m => m.MemberId == memberId);
    }

    [Fact]
    public async Task GetAvailableFamilyMembers_ExcludesMembersWithFamily()
    {
        var (memberId, otherMemberId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            // Give member2 a family
            var newUser = new User("fam@test.com", "fam@test.com", "Fam", "Test", new DateOnly(2000, 1, 1), Gender.Male);
            ctx.Add(newUser);
            await ctx.SaveChangesAsync();
            var newMember = new ClubMember(newUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(newMember);
            await ctx.SaveChangesAsync();

            var family = new Domain.Families.Family(club.Id, [member2.Id], [newMember.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return (member1.Id, newMember.Id);
        });

        var result = await SendAsync(new GetAvailableFamilyMembers(memberId));

        result.AvailableMembers.Should().NotContain(m => m.MemberId == otherMemberId);
    }
}
