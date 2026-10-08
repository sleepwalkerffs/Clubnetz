using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class DeleteFamilyMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteFamilyMember_RemovesChildFromFamily()
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

        var family = await QueryAsync(ctx => ctx.Families
            .Include(f => f.Children)
            .SingleAsync(f => f.Id == familyId));
        family.Children.Should().BeEmpty();
    }
}
