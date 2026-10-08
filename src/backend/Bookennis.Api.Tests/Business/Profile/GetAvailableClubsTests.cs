using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class GetAvailableClubsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAvailableClubs_ReturnsAtLeastOneClub()
    {
        var result = await SendAsync(new GetAvailableClubs());

        result.Clubs.Should().NotBeEmpty();
        result.Clubs.Should().Contain(c => c.Name == "TestClub");
    }
}
