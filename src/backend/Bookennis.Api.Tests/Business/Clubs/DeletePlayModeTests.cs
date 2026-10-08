using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class DeletePlayModeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeletePlayMode_RemovesPlayModeFromClub()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        // Add a play mode first so we can delete it without affecting seeded data
        await SendAsync(new AddPlayMode("ToDelete", [MemberRole.User], System.Drawing.Color.Gray.ToArgb(), 2, false, TimeSpan.FromMinutes(45), false, false));

        var playModeId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.PlayModes).SingleAsync();
            return club.PlayModes.Single(p => p.Name == "ToDelete").Id;
        });

        await SendAsync(new DeletePlayMode(playModeId));

        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.PlayModes).SingleAsync());
        club.PlayModes.Should().NotContain(p => p.Id == playModeId);
    }

    [Fact]
    public async Task DeletePlayMode_NonExistentPlayMode_ThrowsInvalidOperationException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var act = () => SendAsync(new DeletePlayMode(999999));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
