using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class GetFamiliesTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetFamilies_NoFamilies_ReturnsEmptyList()
    {
        var result = await SendAsync(new GetFamilies());

        result.Families.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFamilies_WithFamily_ReturnsFamilies()
    {
        await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], [member2.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetFamilies());

        result.Families.Should().HaveCount(1);
        result.Families[0].Parents.Should().HaveCount(1);
        result.Families[0].Children.Should().HaveCount(1);
    }
}
