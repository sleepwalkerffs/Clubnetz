using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class GetPlayModesTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetPlayModes_ReturnsPlayModesForClub()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var result = await SendAsync(new GetPlayModes());

        result.PlayModes.Should().HaveCountGreaterThanOrEqualTo(1);
        result.PlayModes.Should().Contain(p => p.Name == "Single");
    }
}
