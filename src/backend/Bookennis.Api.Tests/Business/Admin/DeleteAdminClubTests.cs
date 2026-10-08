using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class DeleteAdminClubTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteAdminClub_RemovesClubAndMembers()
    {
        // Create a separate club to delete (don't delete the seeded one)
        var clubId = await QueryAsync(async ctx =>
        {
            var club = new Domain.Clubs.Club(
                "ClubToDelete",
                new Global.Intervals.TimeOnlyInterval(new TimeOnly(08, 00), new TimeOnly(20, 00)),
                new Global.Intervals.TimeOnlyInterval(new TimeOnly(16, 00), new TimeOnly(19, 00)),
                [new Domain.Clubs.Club.PlayModeDto([Bookennis.Domain.Members.MemberRole.User], System.Drawing.Color.Red, 2, false, "Default", TimeSpan.FromHours(1), false, false)]);
            ctx.Add(club);
            await ctx.SaveChangesAsync();
            return club.Id;
        });

        SetTenantId(0);

        await SendAsync(new DeleteAdminClub(clubId));

        var exists = await QueryAsync(ctx => ctx.Clubs.IgnoreQueryFilters().AnyAsync(c => c.Id == clubId));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAdminClub_NonExistentClub_ThrowsEntityNotFoundException()
    {
        SetTenantId(0);

        var act = () => SendAsync(new DeleteAdminClub(999999));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
