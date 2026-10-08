using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminUserTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminUser_ReturnsUserDetails()
    {
        var user = Query(ctx => ctx.TestData().User);

        var result = await SendAsync(new GetAdminUser(user.Id));

        result.Id.Should().Be(user.Id);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
        result.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetAdminUser_NonExistentUser_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new GetAdminUser(999999));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
