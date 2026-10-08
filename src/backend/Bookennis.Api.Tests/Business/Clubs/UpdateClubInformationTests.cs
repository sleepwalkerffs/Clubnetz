using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class UpdateClubInformationTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateClubInformation_ValidHours_UpdatesSuccessfully()
    {
        var club = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();
            return ctx.TestData().Club;
        });
        var newOpening = new TimeOnlyInterval(new TimeOnly(06, 00), new TimeOnly(23, 00));
        var newPrime = new TimeOnlyInterval(new TimeOnly(17, 00), new TimeOnly(21, 00));
        var newSettings = new PrimeTimeSettings(PrimeTimeSettings.Default.IsEnabled, newPrime, PrimeTimeSettings.Default.ApplicableWeekdays, PrimeTimeSettings.Default.RestrictChildren, PrimeTimeSettings.Default.RestrictGuests, PrimeTimeSettings.Default.ChildAgeThreshold);

        await SendAsync(new UpdateClubInformation(club.Id, newOpening, newSettings, 30));

        var updated = Query(ctx => ctx.TestData().Club);
        updated.OpeningHours.Should().Be(newOpening);
        updated.PrimeTimeSettings.PrimeTimeHours.Should().Be(newPrime);
        updated.BookingGracePeriodInMinutes.Should().Be(30);
    }

    [Fact]
    public async Task UpdateClubInformation_PrimeTimeOutsideOpening_ThrowsPreconditionException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var club = Query(ctx => ctx.TestData().Club);
        var opening = new TimeOnlyInterval(new TimeOnly(10, 00), new TimeOnly(18, 00));
        var primeOutside = new TimeOnlyInterval(new TimeOnly(17, 00), new TimeOnly(21, 00));

        var primeSettings = new PrimeTimeSettings(PrimeTimeSettings.Default.IsEnabled, primeOutside, PrimeTimeSettings.Default.ApplicableWeekdays, PrimeTimeSettings.Default.RestrictChildren, PrimeTimeSettings.Default.RestrictGuests, PrimeTimeSettings.Default.ChildAgeThreshold);

        var act = () => SendAsync(new UpdateClubInformation(club.Id, opening, primeSettings, null));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
