using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class GetFamilyTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetFamily_ReturnsCorrectFamilyDetails()
    {
        var familyId = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], [member2.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return family.Id;
        });

        var result = await SendAsync(new GetFamily(familyId));

        result.FamilyId.Should().Be(familyId);
        result.Parents.Should().HaveCount(1);
        result.Children.Should().HaveCount(1);
    }
}
