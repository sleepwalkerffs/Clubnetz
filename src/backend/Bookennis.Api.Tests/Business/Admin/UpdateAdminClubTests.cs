using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
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
    public async Task UpdateAdminClub_SetsTheRegion_AndKeepsItWhenTheRequestHasNone()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);
        var club = await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId));
        var prime = club.PrimeTimeSettings.PrimeTimeHours;

        await SendAsync(new UpdateAdminClub(clubId, club.Name, club.OpeningHours, prime, club.PrimeTimeSettings, null, false,
            Domain.User.Country.UnitedStates, "America/New_York", club.Currency));
        // An older client sends no region: the club keeps it
        await SendAsync(new UpdateAdminClub(clubId, club.Name, club.OpeningHours, prime, club.PrimeTimeSettings, null, false));

        var updated = await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId));
        updated.Country.Should().Be(Domain.User.Country.UnitedStates);
        updated.TimeZoneId.Should().Be("America/New_York");
    }

    [Theory]
    [InlineData("Eastern Standard Time")] // a Windows id: the app in the browser can't use it
    [InlineData("Asia/Tokyo")] // not a supported zone
    [InlineData("Mars/Olympus")]
    public async Task UpdateAdminClub_RefusesTimeZonesOutsideTheSupportedOnes(string timeZoneId)
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);
        var club = await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId));

        var act = () => SendAsync(new UpdateAdminClub(clubId, club.Name, club.OpeningHours, club.PrimeTimeSettings.PrimeTimeHours, club.PrimeTimeSettings, null, false,
            TimeZoneId: timeZoneId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(Club.RegionErrorCode.ClubInvalidTimeZone));
    }

    [Fact]
    public async Task UpdateAdminClub_CurrencyCanChangeOnlyWhileTheClubHasNoPrices()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);
        var club = await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId));
        Task Change(string currency) => SendAsync(new UpdateAdminClub(clubId, club.Name, club.OpeningHours, club.PrimeTimeSettings.PrimeTimeHours, club.PrimeTimeSettings, null, false,
            Currency: currency));

        await Change("usd");
        (await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId))).Currency.Should().Be("usd");

        await QueryAsync(async ctx =>
        {
            ctx.ClubhouseItems.Add(new ClubhouseItem(clubId, new ClubhouseItem.ItemData("Lemonade", ClubhouseItemCategory.Drink, 300, true, 0), "usd"));
            return await ctx.SaveChangesAsync();
        });

        var act = () => Change("eur");

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(Club.RegionErrorCode.ClubCurrencyInUse));
        (await QueryAsync(ctx => ctx.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId))).Currency.Should().Be("usd");
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
