using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class GetBookingsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetBookings_ReturnsBookingsInDateRange()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var fromToday = new DateTimeOffset(today.Year, today.Month, today.Day, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(fromToday, fromToday.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));

            var fromYesterday = fromToday.AddDays(-1);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(fromYesterday, fromYesterday.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetBookings(today, today));

        result.Bookings.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBookings_NoBookingsInRange_ReturnsEmptyList()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

        var result = await SendAsync(new GetBookings(futureDate, futureDate));

        result.Bookings.Should().BeEmpty();
    }
}
