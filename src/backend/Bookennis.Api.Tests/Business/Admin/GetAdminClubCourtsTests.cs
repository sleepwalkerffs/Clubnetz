using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminClubCourtsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminClubCourts_ReturnsCourtsForClub()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubCourts(clubId));

        result.Should().HaveCount(2);
        result.Should().BeInAscendingOrder(c => c.SortOrder);
    }

    [Fact]
    public async Task GetAdminClubCourts_NonExistentClub_ReturnsEmptyList()
    {
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubCourts(999999));

        result.Should().BeEmpty();
    }
}
