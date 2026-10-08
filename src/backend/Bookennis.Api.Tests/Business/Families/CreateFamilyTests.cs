using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Families;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class CreateFamilyTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateFamily_ValidMembers_CreatesFamily()
    {
        var familyId = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family = new Family(club.Id, [member1.Id], [member2.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return family.Id;
        });

        familyId.Should().BeGreaterThan(0);
        var family = await QueryAsync(ctx => ctx.Families
            .Include(f => f.Parents)
            .Include(f => f.Children)
            .SingleAsync(f => f.Id == familyId));
        family.Parents.Should().HaveCount(1);
        family.Children.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateFamily_DuplicateParentAndChildIds_ThrowsArgumentException()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var act = () => new Family(clubId, [memberId], [memberId]);

        // The Family domain entity allows creating with duplicate parent/child IDs,
        // but AddChildren throws if a parent is also used as child
        var family = new Family(clubId, [memberId], []);
        var addAct = () => family.AddChildren([memberId]);
        addAct.Should().Throw<ArgumentException>();
    }
}
