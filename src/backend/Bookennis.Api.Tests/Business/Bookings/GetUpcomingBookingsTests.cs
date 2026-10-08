using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class GetUpcomingBookingsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetUpcomingBookings_ReturnsFutureBookingsForUser()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var futureFrom = DateTimeOffset.UtcNow.AddHours(2);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(futureFrom, futureFrom.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));

            var pastFrom = DateTimeOffset.UtcNow.AddHours(-2);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(pastFrom, pastFrom.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetUpcomingBookings(null, userId));

        result.Bookings.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Bookings.Should().AllSatisfy(b => b.Interval.From.Should().BeAfter(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task GetUpcomingBookings_NoFutureBookings_ReturnsEmpty()
    {
        var userId = Query(ctx => ctx.TestData().Admin.Id);

        var result = await SendAsync(new GetUpcomingBookings(null, userId));

        result.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBookings_ReturnsBookingsWhereUserWasAddedAsParticipant()
    {
        // Use Admin (Member2) who has no own bookings — Member1 books and adds Admin as participant
        var adminUserId = Query(ctx => ctx.TestData().Admin.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            var futureFrom = DateTimeOffset.UtcNow.AddHours(2);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(futureFrom, futureFrom.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id, member2.Id]));
            await ctx.SaveChangesAsync();
        });

        // Admin (member2) was added as participant — they should see this booking
        var result = await SendAsync(new GetUpcomingBookings(null, adminUserId));

        result.Bookings.Should().HaveCount(1);
        result.Bookings[0].Interval.From.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
