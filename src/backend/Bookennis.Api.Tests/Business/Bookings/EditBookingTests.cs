using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class EditBookingTests : TestBase
{
    private readonly IBookingDomainService bookingService;

    public EditBookingTests(TestFixture fixture) : base(fixture)
    {
        bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();
    }

    [Fact]
    public async Task EditBooking_UpdatesExistingBooking()
    {
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddHours(2);
        var to = from.AddMinutes(90);
        var newFrom = from.AddMinutes(30);
        var newTo = to.AddMinutes(30);
        var bookingInterval = new DateTimeOffsetInterval(newFrom, newTo);

        var (bookingId, club, court, playMode, member1, user) = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var member1 = ctx.TestData().Member1;
            var user1 = ctx.TestData().User;
            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];
            var court = ctx.TestData().Court1;

            var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, to), TimeZoneInfo.Local.Id, [member1.Id], "original");
            ctx.Add(booking);
            await ctx.SaveChangesAsync();

            return (booking.Id, club, court, playMode, member1, user1);
        });

        SetupBookingService(bookingId, court.Id, playMode.Id, bookingInterval);

        await SendAsync(new EditBooking(
            bookingId,
            court.Id,
            bookingInterval,
            TimeZoneInfo.Local.Id,
            playMode.Id,
            [member1.Id],
            Comment: "updated",
            ClientTimeZoneOffset: null,
            member1.UserId));

        bookingService.Received(1).EditBooking(
            Arg.Any<BookingDomainService.Context>(),
            Arg.Any<Booking>(),
            Arg.Any<DateTimeOffsetInterval>(),
            Arg.Any<string>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task EditBooking_PastBooking_ThrowsPreconditionException()
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

        var (court, playMode, member1) = Query(ctx =>
        {
            var court = ctx.TestData().Court1;
            var playMode = ctx.TestData().Club.PlayModes[0];
            var member1 = ctx.TestData().Member1;
            return (court, playMode, member1);
        });

        var newFrom = DateTimeOffset.UtcNow.AddHours(2);
        var newTo = newFrom.AddMinutes(90);

        var act = () => SendAsync(new EditBooking(
            bookingId,
            court.Id,
            new DateTimeOffsetInterval(newFrom, newTo),
            TimeZoneInfo.Local.Id,
            playMode.Id,
            [member1.Id],
            Comment: null,
            ClientTimeZoneOffset: null,
            member1.UserId));

        await act.Should().ThrowAsync<PreconditionException>();
    }

    [Fact]
    public async Task EditBooking_ExcludesSelfFromIntersectionChecks()
    {
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddHours(2);
        var to = from.AddMinutes(90);

        var (bookingId, club, court, playMode, member1) = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var member1 = ctx.TestData().Member1;
            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];
            var court = ctx.TestData().Court1;

            var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, to), TimeZoneInfo.Local.Id, [member1.Id]);
            ctx.Add(booking);
            await ctx.SaveChangesAsync();

            return (booking.Id, club, court, playMode, member1);
        });

        BookingDomainService.Context? capturedContext = null;
        bookingService.When(x => x.EditBooking(
            Arg.Any<BookingDomainService.Context>(),
            Arg.Any<Booking>(),
            Arg.Any<DateTimeOffsetInterval>(),
            Arg.Any<string>(),
            Arg.Any<string?>()))
            .Do(x => capturedContext = x.Arg<BookingDomainService.Context>());

        await SendAsync(new EditBooking(
            bookingId,
            court.Id,
            new DateTimeOffsetInterval(from, to),
            TimeZoneInfo.Local.Id,
            playMode.Id,
            [member1.Id],
            Comment: null,
            ClientTimeZoneOffset: null,
            member1.UserId));

        capturedContext.Should().NotBeNull();
        capturedContext!.IntersectingBookings.Should().NotContain(b => b.Id == bookingId);
    }

    private void SetupBookingService(int bookingId, int courtId, int playModeId, DateTimeOffsetInterval interval)
    {
        bookingService.EditBooking(
            Arg.Any<BookingDomainService.Context>(),
            Arg.Any<Booking>(),
            Arg.Any<DateTimeOffsetInterval>(),
            Arg.Any<string>(),
            Arg.Any<string?>());
    }
}
