using System.Drawing;
using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.Business.CourtBlockings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class BookCourtsTests : TestBase
{
    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

    private readonly IBookingDomainService bookingService;
    private readonly List<(BookingDomainService.Context Context, DateTimeOffsetInterval Interval)> calls = [];
    private Func<DateTimeOffsetInterval, PreconditionException?> rejects = _ => null;

    public BookCourtsTests(TestFixture fixture) : base(fixture)
    {
        bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();
        bookingService.BookCourt(default!, default!, default!, default).ReturnsForAnyArgs(call =>
        {
            var context = call.ArgAt<BookingDomainService.Context>(0);
            var interval = call.ArgAt<DateTimeOffsetInterval>(1);
            calls.Add((context, interval));

            if (rejects(interval) is { } exception)
                throw exception;

            return new Booking(context.ClubId, context.Court.Id, context.PlayMode.Id, interval, call.ArgAt<string>(2), context.Participants.ConvertAll(p => p.Member.Id), call.ArgAt<string?>(3));
        });
    }

    [Fact]
    public async Task DryRun_SavesNothingAndReportsEverySlot()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        var result = await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10), Slot(TestDataSeed.Court2Id, Day, 10), Slot(TestDataSeed.Court1Id, Day.AddDays(1), 10)], dryRun: true));

        result.Slots.Should().BeEquivalentTo(
            [new BookingSlotCheck(0, null, []), new BookingSlotCheck(1, null, []), new BookingSlotCheck(2, null, [])],
            o => o.WithStrictOrdering());
        result.CreatedBookingIds.Should().BeEmpty();
        result.BookingsToRemove.Should().BeEmpty();
        calls.Should().HaveCount(3);
        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task AllSlotsValid_SavesOneBookingPerSlot()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var slots = new List<BookingSlotModel> { Slot(TestDataSeed.Court1Id, Day, 10), Slot(TestDataSeed.Court2Id, Day, 12), Slot(TestDataSeed.Court1Id, Day.AddDays(1), 9) };

        var result = await SendAsync(Command(playModeId, slots, dryRun: false));

        result.Slots.Should().OnlyContain(s => s.ErrorCode == null);
        result.CreatedBookingIds.Should().HaveCount(3);

        var bookings = await QueryAsync(ctx => ctx.Bookings.Include(b => b.Players).ToListAsync());
        bookings.Should().HaveCount(3);
        for (var i = 0; i < slots.Count; i++)
        {
            var booking = bookings.Single(b => b.Id == result.CreatedBookingIds[i]);
            booking.CourtId.Should().Be(slots[i].CourtId);
            booking.Interval.From.Should().Be(slots[i].From);
            booking.Interval.To.Should().Be(slots[i].To);
            booking.PlayModeId.Should().Be(playModeId);
            booking.Players.Select(p => p.MemberId).Should().Equal(TestDataSeed.Member1Id);
        }
    }

    [Fact]
    public async Task OneSlotInvalid_DryRunReportsIt_RealRunSavesNothingAndThrowsTheErrorOfTheSlot()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var occupied = Slot(TestDataSeed.Court2Id, Day, 12);
        rejects = interval => interval.From == occupied.From
            ? new PreconditionException(BookingDomainService.ErrorCode.CourtOccupied, "occupied")
            : null;
        var slots = new List<BookingSlotModel> { Slot(TestDataSeed.Court1Id, Day, 10), occupied, Slot(TestDataSeed.Court1Id, Day, 14) };

        var dryRun = await SendAsync(Command(playModeId, slots, dryRun: true));
        dryRun.Slots.Select(s => s.ErrorCode).Should().Equal(null, nameof(BookingDomainService.ErrorCode.CourtOccupied), null);

        var act = () => SendAsync(Command(playModeId, slots, dryRun: false));
        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));

        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task ErrorDetailsOfTheDomainAreReturnedPerSlot()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        rejects = _ => new PreconditionException(BookingDomainService.ErrorCode.CourtBlocked, ["Club championship"], "blocked");

        var result = await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10)], dryRun: true));

        result.Slots.Single().Should().BeEquivalentTo(new BookingSlotCheck(0, nameof(BookingDomainService.ErrorCode.CourtBlocked), ["Club championship"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlotsOverlapOnTheSameCourt_AreRejected_AlsoForAnOverbookingPlayMode(bool canOverbook)
    {
        var playModeId = canOverbook ? await AddPlayMode(canOverbook: true) : Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var slots = new List<BookingSlotModel>
        {
            Slot(TestDataSeed.Court1Id, Day, 10, 12),
            Slot(TestDataSeed.Court1Id, Day, 11, 13), // overlaps slot 1
            Slot(TestDataSeed.Court2Id, Day, 11, 13), // other court
            Slot(TestDataSeed.Court1Id, Day, 12, 13), // overlaps the second one
            Slot(TestDataSeed.Court1Id, Day, 13, 14), // starts when the others end
        };

        var result = await SendAsync(Command(playModeId, slots, dryRun: true));

        result.Slots.Select(s => s.ErrorCode).Should().Equal(
            null, nameof(BookCourts.ErrorCode.BookingSlotsOverlap), null, nameof(BookCourts.ErrorCode.BookingSlotsOverlap), null);
        result.Slots[1].ErrorDetails.Should().Equal("1");
        result.Slots[3].ErrorDetails.Should().Equal("2");
        calls.Select(c => c.Interval.From).Should().Equal(slots[0].From, slots[2].From, slots[4].From);

        var act = () => SendAsync(Command(playModeId, slots, dryRun: false));
        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourts.ErrorCode.BookingSlotsOverlap));
        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task SlotEndsBeforeItStarts_IsAnInvalidTime()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var reversed = Slot(TestDataSeed.Court1Id, Day, 12) with { To = Slot(TestDataSeed.Court1Id, Day, 10).From };

        var result = await SendAsync(Command(playModeId, [reversed], dryRun: true));

        result.Slots.Single().ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.InvalidTime));
        calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(BookCourts.MaxSlots + 1)]
    public async Task TooFewOrTooManySlots_AreRejected(int count)
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var slots = Enumerable.Range(0, count).Select(i => Slot(TestDataSeed.Court1Id, Day.AddDays(i), 10)).ToList();

        var act = () => SendAsync(Command(playModeId, slots, dryRun: true));

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(BookCourts.ErrorCode.InvalidSlotCount));
        exception.ErrorDetails.Should().Equal(BookCourts.MaxSlots.ToString());
    }

    [Fact]
    public async Task MaximumNumberOfSlots_IsAccepted()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var slots = Enumerable.Range(0, BookCourts.MaxSlots).Select(i => Slot(i % 2 == 0 ? TestDataSeed.Court1Id : TestDataSeed.Court2Id, Day.AddDays(i / 2), 10)).ToList();

        var result = await SendAsync(Command(playModeId, slots, dryRun: true));

        result.Slots.Should().HaveCount(BookCourts.MaxSlots).And.OnlyContain(s => s.ErrorCode == null);
    }

    [Fact]
    public async Task CourtOfAnotherClubOrUnknownCourt_IsNotFound()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        var act = () => SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10), Slot(999_999, Day, 10)], dryRun: true));

        await act.Should().ThrowAsync<EntityNotFoundException>();
        calls.Should().BeEmpty();
    }

    [Fact]
    public async Task UserMayNotBookForThePlayers_IsRejected()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);

        var act = () => SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10)], dryRun: true) with { Players = [TestDataSeed.Member2Id] });

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourt.ErrorCode.UserNotAllowedToBook));
    }

    [Fact]
    public async Task ChargingPlayMode_EarlierValidSlotsCountLikeExistingBookings()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id); // charges the booking subscription
        await QueryAsync(ctx => CourtBlockingSeed.SeedBooking(ctx, Day, 7)); // an existing booking of member 1 on that day

        var rejected = Slot(TestDataSeed.Court2Id, Day.AddDays(1), 15);
        rejects = interval => interval.From == rejected.From ? new PreconditionException(BookingDomainService.ErrorCode.CourtOccupied, "occupied") : null;

        await SendAsync(Command(playModeId,
        [
            Slot(TestDataSeed.Court1Id, Day, 10),
            Slot(TestDataSeed.Court2Id, Day, 10), // same time on another court
            rejected, // never saved, so it does not count
            Slot(TestDataSeed.Court1Id, Day.AddDays(1), 10),
        ], dryRun: true));

        var players = calls.Select(c => c.Context.Participants.Single()).ToList();
        players.Select(p => p.UpcomingBookingsCount).Should().Equal(1, 2, 3, 3);
        players.Select(p => p.ActiveBookingsAtTimeOfReservation).Should().Equal(0, 1, 0, 0);
        players.Select(p => p.BookingsOnThatDay).Should().Equal(1, 2, 0, 0);
        players.Select(p => p.PlayModeBookingsInSeasonCount).Should().Equal(0, 0, 0, 0); // the play mode has no maximum per season
    }

    [Fact]
    public async Task PlayModeWithMaximumPerSeason_EarlierSlotsCountTowardsTheQuota()
    {
        var playModeId = await AddPlayMode(isCharging: false, maxBookingsPerSeason: 10);
        var seasonId = await QueryAsync(async ctx =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(today.AddDays(-10), today.AddDays(100)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            ctx.Add(new Booking(TestDataSeed.ClubId, TestDataSeed.Court2Id, playModeId, CourtBlockingSeed.At(Day, 7, 8), TimeZoneInfo.Utc.Id, [TestDataSeed.Member1Id]));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10), Slot(TestDataSeed.Court2Id, Day, 10), Slot(TestDataSeed.Court1Id, Day.AddDays(1), 10)], dryRun: true));

        calls.Should().OnlyContain(c => c.Context.ActiveSeasonId == seasonId);
        var players = calls.Select(c => c.Context.Participants.Single()).ToList();
        players.Select(p => p.PlayModeBookingsInSeasonCount).Should().Equal(1, 2, 3);

        // Bookings of a play mode that does not charge the subscription never count as upcoming, active or on that day
        players.Should().OnlyContain(p => p.UpcomingBookingsCount == 0 && p.ActiveBookingsAtTimeOfReservation == 0 && p.BookingsOnThatDay == 0);
    }

    [Fact]
    public async Task EverySlotGetsTheIntersectingBookingsAndBlockingsOfItsCourtAndTime()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        var day = CourtBlockingSeed.Day;
        var existing = await QueryAsync(async ctx =>
        {
            await CourtBlockingSeed.Seed(ctx); // court 1, 10:00 - 14:00
            return await CourtBlockingSeed.SeedBooking(ctx, day, 16, TestDataSeed.Court2Id);
        });

        await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, day, 12), Slot(TestDataSeed.Court2Id, day, 12), Slot(TestDataSeed.Court2Id, day, 16)], dryRun: true));

        calls.Select(c => c.Context.CourtBlockingTitles.ToList()).Should().BeEquivalentTo(
            new List<List<string>> { new() { CourtBlockingSeed.Title }, new(), new() }, o => o.WithStrictOrdering());
        calls.Select(c => c.Context.IntersectingBookings.Select(b => b.Id).ToList()).Should().BeEquivalentTo(
            new List<List<int>> { new(), new(), new() { existing.Id } }, o => o.WithStrictOrdering());
        calls.Select(c => c.Context.Court.Id).Should().Equal(TestDataSeed.Court1Id, TestDataSeed.Court2Id, TestDataSeed.Court2Id);
    }

    [Fact]
    public async Task OverbookingPlayMode_ListsTheBookingsItRemoves()
    {
        var overbookingPlayModeId = await AddPlayMode(canOverbook: true);
        var existing = await QueryAsync(async ctx =>
        {
            var playModeId = ctx.TestData().Club.PlayModes[0].Id;
            var booking = new Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, CourtBlockingSeed.At(Day, 10, 12), TimeZoneInfo.Utc.Id, [TestDataSeed.Member1Id, TestDataSeed.Member2Id]);
            ctx.Add(booking);
            await ctx.SaveChangesAsync();
            return booking;
        });

        // The existing booking intersects two slots, it is listed once
        var slots = new List<BookingSlotModel> { Slot(TestDataSeed.Court1Id, Day, 9, 11), Slot(TestDataSeed.Court1Id, Day, 11, 13), Slot(TestDataSeed.Court2Id, Day, 10) };
        var result = await SendAsync(Command(overbookingPlayModeId, slots, dryRun: true));

        var removed = result.BookingsToRemove.Should().ContainSingle().Which;
        removed.BookingEntryId.Should().Be(existing.Id);
        removed.CourtId.Should().Be(TestDataSeed.Court1Id);
        removed.Interval.Should().Be(existing.Interval);
        removed.PlayModeName.Should().Be("Single");
        removed.PlayerNames.Should().BeEquivalentTo("ad min", "us er");
    }

    [Fact]
    public async Task PlayModeWithoutOverbooking_ListsNoBookingsToRemove()
    {
        var playModeId = Query(ctx => ctx.TestData().Club.PlayModes[0].Id);
        await QueryAsync(ctx => CourtBlockingSeed.SeedBooking(ctx, Day, 10));
        rejects = _ => new PreconditionException(BookingDomainService.ErrorCode.CourtOccupied, "occupied");

        var result = await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10)], dryRun: true));

        result.BookingsToRemove.Should().BeEmpty();
        result.Slots.Single().ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));
    }

    [Fact]
    public async Task ConfirmedBookingsToRemoveMatch_BookingsAreSaved()
    {
        var (playModeId, existingId) = await SeedOverbookingSituation();

        var result = await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10)], dryRun: false) with { ExpectedBookingIdsToRemove = [existingId] });

        result.CreatedBookingIds.Should().ContainSingle();
        (await QueryAsync(ctx => ctx.Bookings.CountAsync(b => b.PlayModeId == playModeId))).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedBookingsToRemoveDiffer_IsRejectedAndNothingIsSaved(bool confirmedNone)
    {
        var (playModeId, existingId) = await SeedOverbookingSituation();
        List<int> confirmed = confirmedNone ? [] : [existingId, existingId + 1000];

        var act = () => SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10)], dryRun: false) with { ExpectedBookingIdsToRemove = confirmed });

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(BookCourts.ErrorCode.BookingsToRemoveChanged));
        (await QueryAsync(ctx => ctx.Bookings.CountAsync(b => b.PlayModeId == playModeId))).Should().Be(0);
    }

    [Fact]
    public async Task NoConfirmedBookingsToRemove_IsNotChecked()
    {
        var (playModeId, _) = await SeedOverbookingSituation();

        var result = await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10)], dryRun: false));

        result.CreatedBookingIds.Should().ContainSingle();
    }

    [Fact]
    public async Task ConfirmedBookingsToRemove_AreIgnoredForADryRun()
    {
        var (playModeId, existingId) = await SeedOverbookingSituation();

        var result = await SendAsync(Command(playModeId, [Slot(TestDataSeed.Court1Id, Day, 10)], dryRun: true) with { ExpectedBookingIdsToRemove = [] });

        result.BookingsToRemove.Select(b => b.BookingEntryId).Should().Equal(existingId);
    }

    /// <summary>An overbooking play mode and an existing booking on court 1 at 10:00 on <see cref="Day"/>.</summary>
    private async Task<(int PlayModeId, int ExistingBookingId)> SeedOverbookingSituation()
    {
        var playModeId = await AddPlayMode(canOverbook: true);
        var existing = await QueryAsync(ctx => CourtBlockingSeed.SeedBooking(ctx, Day, 10));
        return (playModeId, existing.Id);
    }

    private static BookCourts Command(int playModeId, List<BookingSlotModel> slots, bool dryRun)
        => new(slots, TimeZoneInfo.Utc.Id, playModeId, [TestDataSeed.Member1Id], Comment: null, dryRun, ClientTimeZoneOffset: null, TestDataSeed.UserId);

    private static BookingSlotModel Slot(int courtId, DateOnly day, int fromHour, int? toHour = null)
    {
        var interval = CourtBlockingSeed.At(day, fromHour, toHour ?? fromHour + 1);
        return new BookingSlotModel(courtId, interval.From, interval.To);
    }

    private Task<int> AddPlayMode(bool canOverbook = false, bool isCharging = false, int? maxBookingsPerSeason = null)
        => QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            club.AddPlayMode(new Club.PlayModeDto([MemberRole.SportsDirector], Color.Red, null, isCharging, "Tournament", null, canOverbook, true, maxBookingsPerSeason));
            await ctx.SaveChangesAsync();
            return club.PlayModes.Single(p => p.Name == "Tournament").Id;
        });
}
