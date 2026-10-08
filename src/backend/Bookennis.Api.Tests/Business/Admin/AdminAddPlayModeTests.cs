using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminAddPlayModeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminAddPlayMode_AddsPlayModeToClub()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        await SendAsync(new AdminAddPlayMode(clubId, "Doubles", [MemberRole.User], System.Drawing.Color.Blue.ToArgb(), 4, false, TimeSpan.FromHours(1), false, true));

        SetTenantId(clubId);
        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == clubId));
        club.PlayModes.Should().Contain(p => p.Name == "Doubles");
    }
}
