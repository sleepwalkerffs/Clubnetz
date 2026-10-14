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

    [Fact]
    public async Task GetUserProfile_ReturnsClubsWithNames()
    {
        var (user, club) = Query(ctx => (ctx.TestData().User, ctx.TestData().Club));

        var result = await SendAsync(new GetUserProfile(user.Id));

        result.Clubs.Select(c => c.Id).Should().BeEquivalentTo(result.AvailableClubIds);
        result.Clubs.Should().Contain(c => c.Id == club.Id && c.Name == club.Name && c.Roles.Length > 0);
    }
}
