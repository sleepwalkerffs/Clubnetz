using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Courts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminDeleteCourtTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminDeleteCourt_RemovesCourt()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        // Create a court to delete
        var courtId = await QueryAsync(async ctx =>
        {
            var court = new Court(clubId, "CourtToDelete", "CTD", 100);
            ctx.Courts.Add(court);
            await ctx.SaveChangesAsync();
            return court.Id;
        });

        await SendAsync(new AdminDeleteCourt(clubId, courtId));

        var exists = await QueryAsync(ctx => ctx.Courts.AnyAsync(c => c.Id == courtId));
        exists.Should().BeFalse();
    }
}
