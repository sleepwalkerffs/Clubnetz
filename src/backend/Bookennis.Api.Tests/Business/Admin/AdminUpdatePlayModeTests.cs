using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminUpdatePlayModeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminUpdatePlayMode_UpdatesPlayModeDetails()
    {
        var (clubId, playModeId) = Query(ctx =>
        {
            var club = ctx.TestData().Club;
            return (club.Id, club.PlayModes[0].Id);
        });

        SetTenantId(0);

        await SendAsync(new AdminUpdatePlayMode(clubId, playModeId, "Updated Single", [MemberRole.User, MemberRole.Admin], System.Drawing.Color.Red.ToArgb(), 2, true, TimeSpan.FromHours(2), true, true));

        SetTenantId(clubId);
        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == clubId));
        var playMode = club.PlayModes.Single(p => p.Id == playModeId);
        playMode.Name.Should().Be("Updated Single");
        playMode.FixedDuration.Should().Be(TimeSpan.FromHours(2));
        playMode.CanOverbook.Should().BeTrue();
        playMode.CommentAllowed.Should().BeTrue();
    }
}
