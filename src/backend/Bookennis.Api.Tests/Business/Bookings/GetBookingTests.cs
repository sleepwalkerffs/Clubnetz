using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class GetBookingTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetBooking_ReturnsCorrectBooking()
    {
        var (bookingId, courtId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var from = DateTimeOffset.UtcNow.AddHours(1);
            var to = from.AddMinutes(90);
            var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, to), TimeZoneInfo.Local.Id, [member1.Id], "Test comment");
            ctx.Add(booking);
            await ctx.SaveChangesAsync();
            return (booking.Id, court.Id);
        });

        var result = await SendAsync(new GetBooking(bookingId));

        result.BookingEntryId.Should().Be(bookingId);
        result.CourtId.Should().Be(courtId);
        result.Comment.Should().Be("Test comment");
        result.Players.Should().HaveCount(1);
    }
}
