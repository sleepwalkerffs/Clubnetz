using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class DeleteRecurringBookingTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteRecurring_SingleScope_DeletesOnlyOneBooking()
    {
        var (bookingId, seriesId) = await SetupRecurringSeries();

        await SendAsync(new DeleteRecurringBooking(bookingId, RecurringDeleteScope.Single));

        var remainingBookings = await QueryAsync(ctx => ctx.Bookings
            .Where(b => b.RecurringBookingSeriesId == seriesId)
            .CountAsync());
        remainingBookings.Should().Be(2); // 3 - 1 = 2

        // Series still exists
        var seriesExists = await QueryAsync(ctx => ctx.RecurringBookingSeries.AnyAsync(s => s.Id == seriesId));
        seriesExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteRecurring_ThisAndFollowingScope_DeletesFromBookingOnward()
    {
        var (_, seriesId) = await SetupRecurringSeries();

        // Get the second booking (middle one)
        var secondBooking = await QueryAsync(ctx => ctx.Bookings
            .Where(b => b.RecurringBookingSeriesId == seriesId)
            .OrderBy(b => b.Interval.From)
            .Skip(1)
            .FirstAsync());

        await SendAsync(new DeleteRecurringBooking(secondBooking.Id, RecurringDeleteScope.ThisAndFollowing));

        // Only the first booking should remain
        var remainingBookings = await QueryAsync(ctx => ctx.Bookings
            .Where(b => b.RecurringBookingSeriesId == seriesId)
            .CountAsync());
        remainingBookings.Should().Be(1);

        // Series should still exist with updated end date
        var series = await QueryAsync(ctx => ctx.RecurringBookingSeries.SingleAsync(s => s.Id == seriesId));
        series.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteRecurring_ThisAndFollowingScope_FirstBooking_DeletesEntireSeries()
    {
        var (bookingId, seriesId) = await SetupRecurringSeries();

        await SendAsync(new DeleteRecurringBooking(bookingId, RecurringDeleteScope.ThisAndFollowing));

        // All bookings deleted
        var remainingBookings = await QueryAsync(ctx => ctx.Bookings
            .Where(b => b.RecurringBookingSeriesId == seriesId)
            .CountAsync());
        remainingBookings.Should().Be(0);

        // Series deleted
        var seriesExists = await QueryAsync(ctx => ctx.RecurringBookingSeries.AnyAsync(s => s.Id == seriesId));
        seriesExists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRecurring_PastBooking_ThrowsPreconditionException()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var series = new RecurringBookingSeries(club.Id, court.Id, playMode.Id, DayOfWeek.Monday,
                new TimeOnly(10, 0), new TimeOnly(11, 0), TimeZoneInfo.Utc.Id, 1,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), null, null, [member.Id]);
            ctx.Add(series);
            await ctx.SaveChangesAsync();

            var from = DateTimeOffset.UtcNow.AddHours(-5);
            var to = from.AddHours(1);
            var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, to), TimeZoneInfo.Utc.Id, [member.Id], null, series.Id);
            ctx.Add(booking);
            await ctx.SaveChangesAsync();

            return booking.Id;
        });

        var act = () => SendAsync(new DeleteRecurringBooking(bookingId, RecurringDeleteScope.Single));
        await act.Should().ThrowAsync<PreconditionException>();
    }

    private async Task<(int bookingId, int seriesId)> SetupRecurringSeries()
    {
        return await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
            var series = new RecurringBookingSeries(club.Id, court.Id, playMode.Id, startDate.DayOfWeek,
                new TimeOnly(10, 0), new TimeOnly(11, 0), TimeZoneInfo.Utc.Id, 1,
                startDate, startDate.AddDays(14), null, [member.Id]);
            ctx.Add(series);
            await ctx.SaveChangesAsync();

            // Create 3 weekly bookings
            var bookings = new List<Booking>();
            for (int i = 0; i < 3; i++)
            {
                var date = startDate.AddDays(i * 7);
                var from = new DateTimeOffset(date.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
                var to = from.AddHours(1);
                var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, to), TimeZoneInfo.Utc.Id, [member.Id], null, series.Id);
                ctx.Add(booking);
                bookings.Add(booking);
            }
            await ctx.SaveChangesAsync();

            return (bookings[0].Id, series.Id);
        });
    }
}
