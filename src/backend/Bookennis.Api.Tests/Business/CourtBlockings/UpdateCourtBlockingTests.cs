using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class UpdateCourtBlockingTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateCourtBlocking_ReplacesDataCourtsAndOccurrences()
    {
        var id = await QueryAsync(async ctx => (await CourtBlockingSeed.Seed(ctx)).Id);

        var data = CourtBlockingSeed.Data(CourtBlockingSeed.Day.AddDays(1), [TestDataSeed.Court2Id]) with { Title = "Maintenance", StartTime = null, EndTime = null };
        await SendAsync(new UpdateCourtBlocking(TestDataSeed.ClubId, id, data, DeleteConflictingBookings: false));

        var blocking = await QueryAsync(ctx => ctx.CourtBlockings
            .Include(b => b.Courts)
            .Include(b => b.Occurrences)
            .SingleAsync(b => b.Id == id));

        blocking.Title.Should().Be("Maintenance");
        blocking.StartTime.Should().BeNull();
        blocking.Courts.Select(c => c.CourtId).Should().Equal(TestDataSeed.Court2Id);
        blocking.Occurrences.Select(o => o.Interval).Should().Equal(CourtBlockingSeed.AllDay(CourtBlockingSeed.Day.AddDays(1)));
    }

    [Fact]
    public async Task UpdateCourtBlocking_ExtendedIntoABooking_HasToBeConfirmed()
    {
        var id = await QueryAsync(async ctx =>
        {
            await CourtBlockingSeed.SeedBooking(ctx, CourtBlockingSeed.Day, 15);
            return (await CourtBlockingSeed.Seed(ctx)).Id;
        });

        var data = CourtBlockingSeed.Data() with { EndTime = new TimeOnly(16, 0) };
        var act = () => SendAsync(new UpdateCourtBlocking(TestDataSeed.ClubId, id, data, DeleteConflictingBookings: false));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(CourtBlocking.ErrorCode.CourtBlockingHasConflictingBookings));

        var endTime = await QueryAsync(ctx => ctx.CourtBlockings.Where(b => b.Id == id).Select(b => b.EndTime).SingleAsync());
        endTime.Should().Be(new TimeOnly(14, 0));

        await SendAsync(new UpdateCourtBlocking(TestDataSeed.ClubId, id, data, DeleteConflictingBookings: true));

        endTime = await QueryAsync(ctx => ctx.CourtBlockings.Where(b => b.Id == id).Select(b => b.EndTime).SingleAsync());
        endTime.Should().Be(new TimeOnly(16, 0));
    }

    [Fact]
    public async Task UpdateCourtBlocking_OnlyRenamed_NeedsNoConfirmation()
    {
        var id = await QueryAsync(async ctx =>
        {
            var blocking = await CourtBlockingSeed.Seed(ctx);

            // E.g. a booking that was not deleted because the blocking was created while it was already running
            await CourtBlockingSeed.SeedBooking(ctx, CourtBlockingSeed.Day, 11);
            return blocking.Id;
        });

        await SendAsync(new UpdateCourtBlocking(TestDataSeed.ClubId, id, CourtBlockingSeed.Data() with { Title = "Renamed" }, DeleteConflictingBookings: false));

        (await QueryAsync(ctx => ctx.CourtBlockings.Where(b => b.Id == id).Select(b => b.Title).SingleAsync())).Should().Be("Renamed");
    }

    [Fact]
    public async Task UpdateCourtBlocking_BlockingOfAnotherClub_ThrowsEntityNotFoundException()
    {
        var id = await QueryAsync(async ctx => (await CourtBlockingSeed.Seed(ctx)).Id);

        var act = () => SendAsync(new UpdateCourtBlocking(TestDataSeed.ClubId + 1, id, CourtBlockingSeed.Data(), false));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
