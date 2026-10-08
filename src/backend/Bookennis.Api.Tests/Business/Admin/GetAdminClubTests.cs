using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminClubTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminClub_ReturnsClubDetails()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClub(clubId));

        result.Id.Should().Be(clubId);
        result.Name.Should().Be("TestClub");
        result.PlayModes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAdminClub_NonExistentClub_ThrowsEntityNotFoundException()
    {
        SetTenantId(0);

        var act = () => SendAsync(new GetAdminClub(999999));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
