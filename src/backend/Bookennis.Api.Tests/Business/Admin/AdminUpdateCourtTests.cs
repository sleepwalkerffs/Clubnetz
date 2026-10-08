using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminUpdateCourtTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminUpdateCourt_UpdatesCourtDetails()
    {
        var (clubId, courtId) = Query(ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            return (club.Id, court.Id);
        });

        await SendAsync(new AdminUpdateCourt(clubId, courtId, "Admin Renamed", "AR", 99, null));

        var court = await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId));
        court.Name.Should().Be("Admin Renamed");
        court.Alias.Should().Be("AR");
        court.SortOrder.Should().Be(99);
        court.Inactive.Should().BeNull();
    }

    [Fact]
    public async Task AdminUpdateCourt_SetInactive_SetsInactiveInterval()
    {
        var (clubId, courtId) = Query(ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court2;
            return (club.Id, court.Id);
        });

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(14);
        var inactive = new Global.Intervals.DateTimeOffsetInterval(from, to);

        await SendAsync(new AdminUpdateCourt(clubId, courtId, "Court 2", "C2", 1, inactive));

        var court = await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId));
        court.Inactive.Should().NotBeNull();
    }
}
