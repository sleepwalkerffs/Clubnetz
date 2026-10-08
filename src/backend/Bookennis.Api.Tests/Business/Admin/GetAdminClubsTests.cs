using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminClubsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminClubs_ReturnsClubs()
    {
        SetTenantId(0); // Admin queries are not tenant-scoped

        var result = await SendAsync(new GetAdminClubs());

        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.Name == "TestClub");
    }

    [Fact]
    public async Task GetAdminClubs_ContainsMemberAndPlayModeCount()
    {
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubs());

        var testClub = result.Single(c => c.Name == "TestClub");
        testClub.MemberCount.Should().BeGreaterThanOrEqualTo(2);
        testClub.PlayModeCount.Should().BeGreaterThanOrEqualTo(1);
    }
}
