using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class UpdateAdminClubTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateAdminClub_UpdatesClubDetails()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var newOpening = new TimeOnlyInterval(new TimeOnly(06, 00), new TimeOnly(23, 00));
        var newPrime = new TimeOnlyInterval(new TimeOnly(16, 00), new TimeOnly(21, 00));
        var newPrimeTimeSettings = new PrimeTimeSettings(true, newPrime, [DayOfWeek.Monday, DayOfWeek.Wednesday], true, false, 18);

        await SendAsync(new UpdateAdminClub(clubId, "Updated Club", newOpening, newPrime, newPrimeTimeSettings, 20, IsAtpClub: false));

        SetTenantId(clubId);
        var club = await QueryAsync(ctx => ctx.Clubs.SingleAsync(c => c.Id == clubId));
        club.Name.Should().Be("Updated Club");
        club.OpeningHours.Should().Be(newOpening);
        club.PrimeTimeSettings.PrimeTimeHours.Should().Be(newPrime);
        club.BookingGracePeriodInMinutes.Should().Be(20);
        club.PrimeTimeSettings.IsEnabled.Should().BeTrue();
        club.PrimeTimeSettings.ApplicableWeekdays.Should().BeEquivalentTo([DayOfWeek.Monday, DayOfWeek.Wednesday]);
        club.PrimeTimeSettings.RestrictChildren.Should().BeTrue();
        club.PrimeTimeSettings.RestrictGuests.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAdminClub_NonExistentClub_ThrowsEntityNotFoundException()
    {
        SetTenantId(0);

        var opening = new TimeOnlyInterval(new TimeOnly(08, 00), new TimeOnly(22, 00));
        var prime = new TimeOnlyInterval(new TimeOnly(17, 00), new TimeOnly(20, 00));

        var act = () => SendAsync(new UpdateAdminClub(999999, "Missing", opening, prime, PrimeTimeSettings.Default, null, IsAtpClub: false));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
