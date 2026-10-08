using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class AddPlayModeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddPlayMode_AddsPlayModeToClub()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        await SendAsync(new AddPlayMode("Doubles", [MemberRole.User], System.Drawing.Color.Blue.ToArgb(), 4, false, TimeSpan.FromHours(1), false, true));

        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.PlayModes).SingleAsync());
        club.PlayModes.Should().Contain(p => p.Name == "Doubles");
    }
}
