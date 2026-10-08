using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class DeleteBookingTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteBooking_FutureBooking_DeletesSuccessfully()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var from = DateTimeOffset.UtcNow.AddHours(2);
            var to = from.AddMinutes(90);
            var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, to), TimeZoneInfo.Local.Id, [member1.Id]);
            ctx.Add(booking);
            await ctx.SaveChangesAsync();
            return booking.Id;
        });

        await SendAsync(new DeleteBooking(bookingId));

        var exists = await QueryAsync(ctx => ctx.Bookings.AnyAsync(b => b.Id == bookingId));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBooking_PastBooking_ThrowsPreconditionException()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var from = DateTimeOffset.UtcNow.AddHours(-2);
            var to = from.AddMinutes(90);
            var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, to), TimeZoneInfo.Local.Id, [member1.Id]);
            ctx.Add(booking);
            await ctx.SaveChangesAsync();
            return booking.Id;
        });

        var act = () => SendAsync(new DeleteBooking(bookingId));
        await act.Should().ThrowAsync<PreconditionException>();
    }
}
