using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.CourtBlockings.DomainEventHandlers;
using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Courts.Events;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class DeleteBookingsOnCourtBlockingTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateOnly Day = CourtBlockingSeed.Day;

    [Fact]
    public async Task Handle_DeletesUpcomingBookingsInTheBlockedTimeAndNotifiesThePlayers()
    {
        var (blockingId, blockedBookingId, keptBookingIds) = await QueryAsync(async ctx =>
        {
            var blocked = await CourtBlockingSeed.SeedBooking(ctx, Day, 11);
            var adjacent = await CourtBlockingSeed.SeedBooking(ctx, Day, 14);
            var otherCourt = await CourtBlockingSeed.SeedBooking(ctx, Day, 11, TestDataSeed.Court2Id);
            var blocking = await CourtBlockingSeed.Seed(ctx);

            return (blocking.Id, blocked.Id, new[] { adjacent.Id, otherCourt.Id });
        });

        var mediator = Substitute.For<IMediator>();
        var pushNotifier = Substitute.For<IBookingPushNotifier>();
        await Handle(blockingId, mediator, pushNotifier);

        (await QueryAsync(ctx => ctx.Bookings.Select(b => b.Id).ToListAsync())).Should().BeEquivalentTo(keptBookingIds);

        await pushNotifier.Received(1).BookingDeleted(blockedBookingId, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.ClubId == TestDataSeed.ClubId
                && e.Type == ClubEmailType.BookingDeleted
                && e.Recipient == "user@bookennis.com"
                && ((BookingDeletedEmailVariables)e.Variables).Booking.Court == "Court 1"
                && ((BookingDeletedEmailVariables)e.Variables).Booking.DeletedBy == "ad min"
                && ((BookingDeletedEmailVariables)e.Variables).Booking.Reason == "gelöscht, da der Platz gesperrt wurde (Club championship)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BookingsThatAlreadyStarted_AreKept()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var blockingId = await QueryAsync(async ctx =>
        {
            await CourtBlockingSeed.SeedBooking(ctx, yesterday, 11);
            return (await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data() with { StartDate = yesterday })).Id;
        });

        var mediator = Substitute.For<IMediator>();
        await Handle(blockingId, mediator, Substitute.For<IBookingPushNotifier>());

        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(1);
        await mediator.DidNotReceive().Send(Arg.Any<SendClubEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BlockingWasDeleted_DoesNothing()
    {
        await QueryAsync(ctx => CourtBlockingSeed.SeedBooking(ctx, Day, 11));

        await Handle(999_999, Substitute.For<IMediator>(), Substitute.For<IBookingPushNotifier>());

        (await QueryAsync(ctx => ctx.Bookings.CountAsync())).Should().Be(1);
    }

    private Task Handle(int blockingId, IMediator mediator, IBookingPushNotifier pushNotifier)
        => ScopedAsync(() => new DeleteBookingsOnCourtBlocking(GetInstance<AppDbContext>(), mediator, IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId), pushNotifier)
            .Handle(new DomainEvent<CourtBlockedDomainEvent>(typeof(CourtBlocking).GUID, blockingId, new CourtBlockedDomainEvent()), CancellationToken.None));
}
