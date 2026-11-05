using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class DeleteFamilyMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteFamilyMember_LastChildOfOneParent_DissolvesTheFamily()
    {
        var (familyId, childMemberId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], [member2.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return (family.Id, member2.Id);
        });

        await SendAsync(new DeleteFamilyMember(familyId, childMemberId));

        (await QueryAsync(ctx => ctx.Families.AnyAsync(f => f.Id == familyId))).Should().BeFalse();
        (await QueryAsync(ctx => ctx.FamilyMembers.AnyAsync(f => f.FamilyId == familyId))).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFamilyMember_OneOfTwoParents_DissolvesTheFamily()
    {
        var familyId = await QueryAsync(async ctx =>
        {
            var family = new Domain.Families.Family(ctx.TestData().Club.Id, [ctx.TestData().Member1.Id, ctx.TestData().Member2.Id], []);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return family.Id;
        });

        await SendAsync(new DeleteFamilyMember(familyId, TestDataSeed.Member2Id));

        (await QueryAsync(ctx => ctx.Families.AnyAsync(f => f.Id == familyId))).Should().BeFalse();
    }
}
