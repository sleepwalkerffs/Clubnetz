using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminOverviewTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminOverview_CountsClubsAndUsers()
    {
        SetTenantId(0); // Admin queries are not tenant-scoped

        var result = await SendAsync(new GetAdminOverview());

        result.ClubCount.Should().Be(1);
        result.UserCount.Should().Be(2);
        result.UnconfirmedUserCount.Should().Be(2);
        result.NewUsersLast30Days.Should().Be(2);
        result.BookingsLast30Days.Should().Be(0);
        result.UpcomingBookings.Should().Be(0);
    }

    [Fact]
    public async Task GetAdminOverview_ReturnsRecentUsers()
    {
        SetTenantId(0);

        var result = await SendAsync(new GetAdminOverview());

        result.RecentUsers.Should().HaveCount(2);
        result.RecentUsers.Should().Contain(u => u.Email == "user@bookennis.com");
        result.RecentUsers.Should().OnlyContain(u => !u.EmailConfirmed);
    }

    [Fact]
    public async Task GetAdminOverview_CountsClubsWithoutActiveSeason()
    {
        SetTenantId(0);

        var before = await SendAsync(new GetAdminOverview());
        before.ClubsWithoutActiveSeason.Should().Be(1);

        await QueryAsync(async ctx =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            ctx.Add(new Season(ctx.TestData().Club.Id, new DateOnlyInterval(today.AddDays(-10), today.AddDays(10))));
            await ctx.SaveChangesAsync();
            return 0;
        });

        var after = await SendAsync(new GetAdminOverview());
        after.ClubsWithoutActiveSeason.Should().Be(0);
    }
}
