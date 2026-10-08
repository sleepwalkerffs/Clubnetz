using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class UpdatePlayModeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdatePlayMode_UpdatesPlayModeDetails()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var playModeId = Query(ctx =>
        {
            var club = ctx.TestData().Club;
            return club.PlayModes[0].Id;
        });

        await SendAsync(new UpdatePlayMode(playModeId, "Updated Single", [MemberRole.User, MemberRole.Admin], System.Drawing.Color.Red.ToArgb(), 2, true, TimeSpan.FromHours(2), true, true));

        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.PlayModes).SingleAsync());
        var playMode = club.PlayModes.Single(p => p.Id == playModeId);
        playMode.Name.Should().Be("Updated Single");
        playMode.FixedDuration.Should().Be(TimeSpan.FromHours(2));
        playMode.CanOverbook.Should().BeTrue();
        playMode.CommentAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePlayMode_NonExistentPlayMode_ThrowsInvalidOperationException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var act = () => SendAsync(new UpdatePlayMode(999999, "Nonexistent", [MemberRole.User], System.Drawing.Color.Red.ToArgb(), 2, false, TimeSpan.FromHours(1), false, false));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
