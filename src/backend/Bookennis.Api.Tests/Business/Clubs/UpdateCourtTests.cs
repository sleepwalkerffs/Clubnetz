using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class UpdateCourtTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateCourt_RenamesCourtAndAlias()
    {
        var courtId = Query(ctx => ctx.TestData().Court1.Id);

        await SendAsync(new UpdateCourt(courtId, "Renamed Court", "RC", null, 5));

        var court = await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId));
        court.Name.Should().Be("Renamed Court");
        court.Alias.Should().Be("RC");
        court.SortOrder.Should().Be(5);
        court.Inactive.Should().BeNull();
    }

    [Fact]
    public async Task UpdateCourt_SetInactive_SetsInactiveInterval()
    {
        var courtId = Query(ctx => ctx.TestData().Court2.Id);
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(7);
        var inactive = new Global.Intervals.DateTimeOffsetInterval(from, to);

        await SendAsync(new UpdateCourt(courtId, "Court 2", "C2", inactive, 1));

        var court = await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId));
        court.Inactive.Should().NotBeNull();
    }
}
