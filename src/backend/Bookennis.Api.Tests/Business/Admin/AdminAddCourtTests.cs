using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminAddCourtTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminAddCourt_CreatesCourtSuccessfully()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        await SendAsync(new AdminAddCourt(clubId, "New Court", "NC", 10));

        var court = await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Name == "New Court"));
        court.ClubId.Should().Be(clubId);
        court.Alias.Should().Be("NC");
        court.SortOrder.Should().Be(10);
    }
}
