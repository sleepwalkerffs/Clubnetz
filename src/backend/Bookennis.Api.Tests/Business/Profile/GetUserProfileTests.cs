using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class GetUserProfileTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetUserProfile_ReturnsCorrectProfile()
    {
        var user = Query(ctx => ctx.TestData().User);

        var result = await SendAsync(new GetUserProfile(user.Id));

        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
        result.Email.Should().Be(user.Email);
        result.AvailableClubIds.Should().NotBeEmpty();
    }
}
