using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.Business.CourtBlockings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

/// <summary>Moving or changing an occurrence of a series (scope Single, or ThisAndFollowing for the edited one) runs the same rules as <see cref="EditBooking"/>.</summary>
public class EditRecurringBookingTests : TestBase
{
    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

    private readonly IBookingDomainService bookingService;
    private BookingDomainService.Context? captured;

    public EditRecurringBookingTests(TestFixture fixture) : base(fixture)
    {
        bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();
        bookingService
            .When(x => x.EditBooking(Arg.Any<BookingDomainService.Context>(), Arg.Any<Booking>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>()))
            .Do(x => captured = x.Arg<BookingDomainService.Context>());
    }

    [Fact]
    public async Task SingleEdit_ValidatesWithTheDomainAndExcludesTheOccurrenceFromTheSeries()
    {
        var booking = await SeedBooking(CourtBlockingSeed.At(Day, 10, 11), [TestDataSeed.Member1Id]);

        await SendAsync(Command(booking.Id, CourtBlockingSeed.At(Day, 12, 13), [TestDataSeed.Member1Id], TestDataSeed.UserId));

        bookingService.Received(1).EditBooking(Arg.Any<BookingDomainService.Context>(), Arg.Is<Booking>(b => b.Id == booking.Id), CourtBlockingSeed.At(Day, 12, 13), TimeZoneInfo.Utc.Id, null);
        captured!.SkipPastBookingValidation.Should().BeFalse();
        (await QueryAsync(ctx => ctx.Bookings.SingleAsync(b => b.Id == booking.Id))).IsExcludedFromSeries.Should().BeTrue();
    }

    [Fact]
    public async Task SingleEdit_OccurrenceStartedAndUserHasNoElevatedRole_IsRejected()
    {
        var started = DateTimeOffset.UtcNow.AddHours(-2);
        var booking = await SeedBooking(new DateTimeOffsetInterval(started, started.AddHours(1)), [TestDataSeed.Member1Id]);

        var act = () => SendAsync(Command(booking.Id, CourtBlockingSeed.At(Day, 12, 13), [TestDataSeed.Member1Id], TestDataSeed.UserId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(EditBooking.ErrorCode.CannotEditBookingFromThePast));
        captured.Should().BeNull();
        (await QueryAsync(ctx => ctx.Bookings.SingleAsync(b => b.Id == booking.Id))).IsExcludedFromSeries.Should().BeFalse();
    }

    [Fact]
    public async Task SingleEdit_OccurrenceStartedAndUserHasAnElevatedRole_SkipsThePastValidation()
    {
        var started = DateTimeOffset.UtcNow.AddHours(-2);
        var booking = await SeedBooking(new DateTimeOffsetInterval(started, started.AddHours(1)), [TestDataSeed.Member2Id]);

        await SendAsync(Command(booking.Id, new DateTimeOffsetInterval(started, started.AddMinutes(90)), [TestDataSeed.Member2Id], TestDataSeed.AdminId));

        captured!.SkipPastBookingValidation.Should().BeTrue();
    }

    [Fact]
    public async Task SingleEdit_UnknownPlayer_IsNotFoundInsteadOfBeingDropped()
    {
        var booking = await SeedBooking(CourtBlockingSeed.At(Day, 10, 11), [TestDataSeed.Member1Id]);

        var act = () => SendAsync(Command(booking.Id, CourtBlockingSeed.At(Day, 12, 13), [TestDataSeed.Member1Id, 999_999], TestDataSeed.UserId));

        await act.Should().ThrowAsync<EntityNotFoundException>();
        captured.Should().BeNull();
    }

    [Fact]
    public async Task SingleEdit_UserMayNotBookForThePlayers_IsRejected()
    {
        var booking = await SeedBooking(CourtBlockingSeed.At(Day, 10, 11), [TestDataSeed.Member2Id]);

        var act = () => SendAsync(Command(booking.Id, CourtBlockingSeed.At(Day, 12, 13), [TestDataSeed.Member2Id], TestDataSeed.UserId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(EditBooking.ErrorCode.UserNotAllowedToBook));
    }

    [Fact]
    public async Task SingleEdit_TheMovedOccurrenceIsLeftOutOfEveryCountAndOfTheIntersectingBookings()
    {
        // The default play mode charges the subscription and the moved occurrence is upcoming, on that day and overlaps its new time
        var booking = await SeedBooking(CourtBlockingSeed.At(Day, 10, 12), [TestDataSeed.Member1Id]);
        await SeedBooking(CourtBlockingSeed.At(Day.AddDays(2), 10, 11), [TestDataSeed.Member1Id]);
        var occupying = await SeedBooking(CourtBlockingSeed.At(Day, 11, 12), [TestDataSeed.Member2Id], TestDataSeed.Court2Id);

        await SendAsync(Command(booking.Id, CourtBlockingSeed.At(Day, 11, 12), [TestDataSeed.Member1Id], TestDataSeed.UserId, TestDataSeed.Court2Id));

        var player = captured!.Participants.Single();
        player.UpcomingBookingsCount.Should().Be(1, "only the other booking counts");
        player.ActiveBookingsAtTimeOfReservation.Should().Be(0);
        player.BookingsOnThatDay.Should().Be(0);
        player.PlayModeBookingsInSeasonCount.Should().Be(0);
        captured.IntersectingBookings.Select(b => b.Id).Should().Equal(occupying.Id);
        captured.Court.Id.Should().Be(TestDataSeed.Court2Id);
    }

    [Fact]
    public async Task SingleEdit_OnTheSameCourt_TheOccurrenceIsNotItsOwnIntersectingBooking()
    {
        var booking = await SeedBooking(CourtBlockingSeed.At(Day, 10, 12), [TestDataSeed.Member1Id]);

        await SendAsync(Command(booking.Id, CourtBlockingSeed.At(Day, 11, 13), [TestDataSeed.Member1Id], TestDataSeed.UserId));

        captured!.IntersectingBookings.Should().BeEmpty();
    }

    [Fact]
    public async Task SeriesEdit_KeepsTheEditedOccurrenceAndCreatesTheLaterOnesAgain()
    {
        var (first, later) = await SeedSeries([TestDataSeed.Member1Id]);

        await SendAsync(Command(first.Id, CourtBlockingSeed.At(Day, 12, 13), [TestDataSeed.Member1Id], TestDataSeed.UserId, scope: RecurringEditScope.ThisAndFollowing));

        bookingService.Received(1).EditBooking(Arg.Any<BookingDomainService.Context>(), Arg.Is<Booking>(b => b.Id == first.Id), CourtBlockingSeed.At(Day, 12, 13), TimeZoneInfo.Utc.Id, null);
        var bookings = await QueryAsync(ctx => ctx.Bookings.Where(b => b.RecurringBookingSeriesId == first.RecurringBookingSeriesId).OrderBy(b => b.Interval.From).ToListAsync());
        bookings[0].Id.Should().Be(first.Id, "the detail page of the edited occurrence stays valid");
        bookings.Select(b => b.Id).Should().NotIntersectWith(later.Select(b => b.Id));
        bookings.Skip(1).Select(b => b.Interval).Should().Equal(CourtBlockingSeed.At(Day.AddDays(7), 12, 13), CourtBlockingSeed.At(Day.AddDays(14), 12, 13));
    }

    [Fact]
    public async Task SeriesEdit_UserMayNotBookForThePlayers_IsRejectedAndKeepsTheSeries()
    {
        var (first, later) = await SeedSeries([TestDataSeed.Member2Id]);

        var act = () => SendAsync(Command(first.Id, CourtBlockingSeed.At(Day, 12, 13), [TestDataSeed.Member2Id], TestDataSeed.UserId, scope: RecurringEditScope.ThisAndFollowing));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(EditBooking.ErrorCode.UserNotAllowedToBook));
        var ids = await QueryAsync(ctx => ctx.Bookings.Where(b => b.RecurringBookingSeriesId == first.RecurringBookingSeriesId).Select(b => b.Id).ToListAsync());
        ids.Should().BeEquivalentTo(later.Select(b => b.Id).Append(first.Id));
    }

    [Fact]
    public async Task SeriesEdit_MovingTheOccurrenceOntoALaterOccurrence_IsNoConflictWithTheOwnSeries()
    {
        var (first, _) = await SeedSeries([TestDataSeed.Member1Id]);

        await SendAsync(Command(first.Id, CourtBlockingSeed.At(Day.AddDays(7), 10, 11), [TestDataSeed.Member1Id], TestDataSeed.UserId, scope: RecurringEditScope.ThisAndFollowing));

        captured!.IntersectingBookings.Should().BeEmpty();
    }

    private EditRecurringBooking Command(int bookingId, DateTimeOffsetInterval interval, List<int> players, int userId, int courtId = TestDataSeed.Court1Id, RecurringEditScope scope = RecurringEditScope.Single)
        => new(bookingId, courtId, interval, TimeZoneInfo.Utc.Id, TestPlayModeId, players, scope, null, null, null, null, userId);

    /// <summary>A weekly series from <see cref="Day"/> 10-11 with three occurrences.</summary>
    private Task<(Booking First, List<Booking> Later)> SeedSeries(List<int> players)
        => QueryAsync(async ctx =>
        {
            TestPlayModeId = ctx.TestData().Club.PlayModes[0].Id;
            var series = new RecurringBookingSeries(TestDataSeed.ClubId, TestDataSeed.Court1Id, TestPlayModeId, Day.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0),
                TimeZoneInfo.Utc.Id, 1, Day, Day.AddDays(14), null, players);
            ctx.Add(series);
            await ctx.SaveChangesAsync();

            var bookings = Enumerable.Range(0, 3)
                .Select(week => new Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, TestPlayModeId, CourtBlockingSeed.At(Day.AddDays(7 * week), 10, 11), TimeZoneInfo.Utc.Id, players, null, series.Id))
                .ToList();
            ctx.AddRange(bookings);
            await ctx.SaveChangesAsync();
            return (bookings[0], bookings.Skip(1).ToList());
        });

    private int TestPlayModeId { get; set; }

    private Task<Booking> SeedBooking(DateTimeOffsetInterval interval, List<int> players, int courtId = TestDataSeed.Court1Id)
        => QueryAsync(async ctx =>
        {
            TestPlayModeId = ctx.TestData().Club.PlayModes[0].Id;
            var booking = new Booking(TestDataSeed.ClubId, courtId, TestPlayModeId, interval, TimeZoneInfo.Utc.Id, players);
            ctx.Add(booking);
            await ctx.SaveChangesAsync();
            return booking;
        });
}
