using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class CreateCourtBlockingTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateCourtBlocking_StoresBlockingWithCourtsAndOccurrences()
    {
        var data = CourtBlockingSeed.Data(courtIds: [TestDataSeed.Court1Id, TestDataSeed.Court2Id]) with
        {
            RecurrenceIntervalWeeks = 1,
            RecurrenceEndDate = CourtBlockingSeed.Day.AddDays(14),
        };

        var id = await SendAsync(new CreateCourtBlocking(TestDataSeed.ClubId, data, DeleteConflictingBookings: false));

        var blocking = await QueryAsync(ctx => ctx.CourtBlockings
            .Include(b => b.Courts)
            .Include(b => b.Occurrences)
            .SingleAsync(b => b.Id == id));

        blocking.ClubId.Should().Be(TestDataSeed.ClubId);
        blocking.Title.Should().Be(CourtBlockingSeed.Title);
        blocking.Courts.Select(c => c.CourtId).Should().BeEquivalentTo([TestDataSeed.Court1Id, TestDataSeed.Court2Id]);
        blocking.Occurrences.OrderBy(o => o.Interval.From).Select(o => o.Interval).Should().Equal(
            CourtBlockingSeed.At(CourtBlockingSeed.Day, 10, 14),
            CourtBlockingSeed.At(CourtBlockingSeed.Day.AddDays(7), 10, 14),
            CourtBlockingSeed.At(CourtBlockingSeed.Day.AddDays(14), 10, 14));
    }

    [Fact]
    public async Task CreateCourtBlocking_EmptyTitle_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new CreateCourtBlocking(TestDataSeed.ClubId, CourtBlockingSeed.Data() with { Title = "" }, false));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(CourtBlocking.ErrorCode.CourtBlockingTitleRequired));
    }

    [Fact]
    public async Task CreateCourtBlocking_CourtOfAnotherClub_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new CreateCourtBlocking(TestDataSeed.ClubId, CourtBlockingSeed.Data(courtIds: [TestDataSeed.Court1Id, 999_999]), false));

        await act.Should().ThrowAsync<EntityNotFoundException>();
        (await QueryAsync(ctx => ctx.CourtBlockings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task CreateCourtBlocking_UpcomingBookingInBlockedTime_HasToBeConfirmed()
    {
        await QueryAsync(ctx => CourtBlockingSeed.SeedBooking(ctx, CourtBlockingSeed.Day, 13));

        var act = () => SendAsync(new CreateCourtBlocking(TestDataSeed.ClubId, CourtBlockingSeed.Data(), DeleteConflictingBookings: false));

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(CourtBlocking.ErrorCode.CourtBlockingHasConflictingBookings));
        exception.ErrorDetails.Should().Equal("1");
        (await QueryAsync(ctx => ctx.CourtBlockings.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task CreateCourtBlocking_ConfirmedConflicts_StoresBlocking()
    {
        await QueryAsync(ctx => CourtBlockingSeed.SeedBooking(ctx, CourtBlockingSeed.Day, 13));

        var id = await SendAsync(new CreateCourtBlocking(TestDataSeed.ClubId, CourtBlockingSeed.Data(), DeleteConflictingBookings: true));

        (await QueryAsync(ctx => ctx.CourtBlockings.AnyAsync(b => b.Id == id))).Should().BeTrue();
    }

    [Fact]
    public async Task CreateCourtBlocking_BookingsOutsideTheBlocking_NeedNoConfirmation()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await QueryAsync(async ctx =>
        {
            // Adjacent, on another court and already played
            await CourtBlockingSeed.SeedBooking(ctx, CourtBlockingSeed.Day, 14);
            await CourtBlockingSeed.SeedBooking(ctx, CourtBlockingSeed.Day, 11, TestDataSeed.Court2Id);
            await CourtBlockingSeed.SeedBooking(ctx, yesterday, 11);
        });

        var data = CourtBlockingSeed.Data() with { StartDate = yesterday };
        var id = await SendAsync(new CreateCourtBlocking(TestDataSeed.ClubId, data, DeleteConflictingBookings: false));

        (await QueryAsync(ctx => ctx.CourtBlockings.AnyAsync(b => b.Id == id))).Should().BeTrue();
    }
}
