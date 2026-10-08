using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class GetClubInformationTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetClubInformation_ReturnsClubOpeningAndPrimeTimeHours()
    {
        var club = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();
            return ctx.TestData().Club;
        });

        var result = await SendAsync(new GetClubInformation(club.Id));

        result.OpeningHours.Should().Be(club.OpeningHours);
        result.PrimeTimeSettings.PrimeTimeHours.Should().Be(club.PrimeTimeSettings.PrimeTimeHours);
    }
}
