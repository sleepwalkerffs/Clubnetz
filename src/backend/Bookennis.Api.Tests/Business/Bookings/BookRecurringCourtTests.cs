using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.Business.CourtBlockings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class BookRecurringCourtTests : TestBase
{
    private readonly IBookingDomainService bookingService;

    public BookRecurringCourtTests(TestFixture fixture) : base(fixture)
    {
        bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();
    }

    [Fact]
    public async Task BookRecurringCourt_CreatesSeriesAndBookings()
    {
        var (courtId, playModeId, memberId, userId) = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member = ctx.TestData().Member1;
            var user = ctx.TestData().User;

            // Enable recurring on playmode
            var playMode = club.PlayModes[0];
            playMode.Update(playMode.AllowedRoles, playMode.Color, playMode.FixedPlayerCount,
                playMode.IsChargingBookingSubscription, playMode.Name, playMode.FixedDuration,
                playMode.CanOverbook, playMode.CommentAllowed, playMode.MaxBookingsPerSeason, allowRecurring: true);

            // Add active season covering the next 8 weeks
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            ctx.Add(new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(60))));
            await ctx.SaveChangesAsync();

            // Add member season
            ctx.Add(new MemberSeason(member.Id, ctx.Seasons.First().Id));
            await ctx.SaveChangesAsync();

            return (court.Id, playMode.Id, member.Id, user.Id);
        });

        // Configure domain service to succeed
        bookingService.BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo =>
            {
                var interval = callInfo.Arg<DateTimeOffsetInterval>();
                return new Booking(TestDataSeed.ClubId, courtId, playModeId, interval, TimeZoneInfo.Local.Id, [memberId]);
            });

        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, now.Day, 10, 0, 0, TimeSpan.Zero).AddDays(1);
        var to = from.AddHours(1);
        var interval = new DateTimeOffsetInterval(from, to);
        var endDate = DateOnly.FromDateTime(from.DateTime).AddDays(21); // ~3 weeks = 4 occurrences with weekly

        var result = await SendAsync(new BookRecurringCourt(
            courtId,
            interval,
            TimeZoneInfo.Utc.Id,
            playModeId,
            [memberId],
            RecurrenceIntervalWeeks: 1,
            EndDate: endDate,
            Comment: "Weekly game",
            ClientTimeZoneOffset: null,
            userId));

        // Assert series created
        var series = await QueryAsync(ctx => ctx.RecurringBookingSeries.SingleAsync());
        series.CourtId.Should().Be(courtId);
        series.PlayModeId.Should().Be(playModeId);
        series.RecurrenceIntervalWeeks.Should().Be(1);
        series.Comment.Should().Be("Weekly game");

        // Assert bookings created (4 weeks)
        var bookings = await QueryAsync(ctx => ctx.Bookings
            .Where(b => b.RecurringBookingSeriesId == series.Id)
            .OrderBy(b => b.Interval.From)
            .ToListAsync());

        bookings.Should().HaveCount(4);
        bookings.All(b => b.Comment == "Weekly game").Should().BeTrue();
    }

    [Fact]
    public async Task BookRecurringCourt_PlayModeNotAllowRecurring_ThrowsPreconditionException()
    {
        var (courtId, playModeId, memberId, userId) = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member = ctx.TestData().Member1;
            var user = ctx.TestData().User;

            // PlayMode does NOT allow recurring (default)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            ctx.Add(new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(60))));
            await ctx.SaveChangesAsync();

            return (court.Id, club.PlayModes[0].Id, member.Id, user.Id);
        });

        var from = DateTimeOffset.UtcNow.AddDays(1);
        var to = from.AddHours(1);
        var interval = new DateTimeOffsetInterval(from, to);

        var act = () => SendAsync(new BookRecurringCourt(
            courtId, interval, TimeZoneInfo.Utc.Id, playModeId, [memberId],
            RecurrenceIntervalWeeks: 1, EndDate: null, Comment: null, ClientTimeZoneOffset: null, userId));

        await act.Should().ThrowAsync<PreconditionException>()
            .WithMessage("*does not allow recurring*");
    }

    [Fact]
    public async Task BookRecurringCourt_InvalidInterval_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new BookRecurringCourt(
            1, new DateTimeOffsetInterval(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            TimeZoneInfo.Utc.Id, 1, [1],
            RecurrenceIntervalWeeks: 0, EndDate: null, Comment: null, ClientTimeZoneOffset: null, 1));

        await act.Should().ThrowAsync<PreconditionException>()
            .WithMessage("*at least 1*");
    }

    [Fact]
    public async Task BookRecurringCourt_CourtBlockedOnAnOccurrence_SkipsTheOccurrence()
    {
        var firstDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var (courtId, playModeId, memberId, userId) = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member = ctx.TestData().Member1;
            var user = ctx.TestData().User;

            var playMode = club.PlayModes[0];
            playMode.Update(playMode.AllowedRoles, playMode.Color, playMode.FixedPlayerCount,
                playMode.IsChargingBookingSubscription, playMode.Name, playMode.FixedDuration,
                canOverbook: true, playMode.CommentAllowed, playMode.MaxBookingsPerSeason, allowRecurring: true);

            ctx.Add(new Season(club.Id, new DateOnlyInterval(firstDay.AddDays(-30), firstDay.AddDays(60))));
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(member.Id, ctx.Seasons.First().Id));
            await ctx.SaveChangesAsync();

            // Blocks the second occurrence, the other court is still free
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(firstDay.AddDays(7)));
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(firstDay.AddDays(14), [TestDataSeed.Court2Id]));

            return (court.Id, playMode.Id, member.Id, user.Id);
        });

        bookingService.BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo => new Booking(TestDataSeed.ClubId, courtId, playModeId, callInfo.Arg<DateTimeOffsetInterval>(), TimeZoneInfo.Utc.Id, [memberId]));

        await SendAsync(new BookRecurringCourt(
            courtId,
            CourtBlockingSeed.At(firstDay, 10, 11),
            TimeZoneInfo.Utc.Id,
            playModeId,
            [memberId],
            RecurrenceIntervalWeeks: 1,
            EndDate: firstDay.AddDays(21),
            Comment: null,
            ClientTimeZoneOffset: null,
            userId));

        var bookings = await QueryAsync(ctx => ctx.Bookings.AsNoTracking().OrderBy(b => b.Interval.From).Select(b => b.Interval).ToListAsync());

        bookings.Should().Equal(
            CourtBlockingSeed.At(firstDay, 10, 11),
            CourtBlockingSeed.At(firstDay.AddDays(14), 10, 11),
            CourtBlockingSeed.At(firstDay.AddDays(21), 10, 11));
    }
}
