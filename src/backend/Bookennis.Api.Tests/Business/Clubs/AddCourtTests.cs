using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class AddCourtTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddCourt_CreatesCourtInCurrentClubAfterExistingCourts()
    {
        var (clubId, maxSortOrder) = Query(ctx => (ctx.TestData().Club.Id, ctx.Courts.Max(c => c.SortOrder)));

        var courtId = await SendAsync(new AddCourt("  Center Court ", " CC ", IsIndoor: true));

        var court = await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId));
        court.ClubId.Should().Be(clubId);
        court.Name.Should().Be("Center Court");
        court.Alias.Should().Be("CC");
        court.IsIndoor.Should().BeTrue();
        court.Inactive.Should().BeNull();
        court.SortOrder.Should().Be(maxSortOrder + 1);
    }
}
