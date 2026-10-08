using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class CreateAdminClubTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateAdminClub_CreatesClubSuccessfully()
    {
        SetTenantId(0);

        var openingHours = new TimeOnlyInterval(new TimeOnly(08, 00), new TimeOnly(22, 00));
        var primeTimeHours = new TimeOnlyInterval(new TimeOnly(17, 00), new TimeOnly(20, 00));

        var clubId = await SendAsync(new CreateAdminClub("New Test Club", openingHours, primeTimeHours, 15));

        clubId.Should().BeGreaterThan(0);

        SetTenantId(clubId);
        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == clubId));
        club.Name.Should().Be("New Test Club");
        club.OpeningHours.Should().Be(openingHours);
        club.PrimeTimeSettings.PrimeTimeHours.Should().Be(primeTimeHours);
        club.BookingGracePeriodInMinutes.Should().Be(15);
        club.PlayModes.Should().HaveCount(1);
    }
}
