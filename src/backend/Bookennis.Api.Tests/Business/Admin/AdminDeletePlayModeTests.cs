using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminDeletePlayModeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminDeletePlayMode_RemovesPlayModeFromClub()
    {
        // Add a play mode first so we can delete it without affecting seeded data
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        await SendAsync(new AdminAddPlayMode(clubId, "ToDelete", [Bookennis.Domain.Members.MemberRole.User], System.Drawing.Color.Gray.ToArgb(), 2, false, TimeSpan.FromMinutes(45), false, false));

        SetTenantId(clubId);
        var playModeId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == clubId);
            return club.PlayModes.Single(p => p.Name == "ToDelete").Id;
        });

        SetTenantId(0);
        await SendAsync(new AdminDeletePlayMode(clubId, playModeId));

        SetTenantId(clubId);
        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == clubId));
        club.PlayModes.Should().NotContain(p => p.Id == playModeId);
    }

    [Fact]
    public async Task AdminDeletePlayMode_NonExistentClub_ThrowsEntityNotFoundException()
    {
        SetTenantId(0);

        var act = () => SendAsync(new AdminDeletePlayMode(999999, 1));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
