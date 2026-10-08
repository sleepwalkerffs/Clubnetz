using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class GetPlayersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetPlayers_ReturnsClubPlayers()
    {
        // Arrange: create an active season and add member1 to it
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(member1.Id, season.Id));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetPlayers());

        result.ClubMembers.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetPlayers_ReturnsProfilePictureUrl()
    {
        var (member1Id, member1UserId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(member1.Id, season.Id));
            ctx.Add(new UserProfilePicture(member1.UserId, [1, 2, 3], "image/jpeg"));
            await ctx.SaveChangesAsync();
            return (member1.Id, member1.UserId);
        });

        var result = await SendAsync(new GetPlayers());

        var player = result.ClubMembers.Single(p => p.MemberId == member1Id);
        player.UserId.Should().Be(member1UserId);
        player.ProfilePictureUrl.Should().Be($"/api/Profile/picture/{member1UserId}");
    }
}
