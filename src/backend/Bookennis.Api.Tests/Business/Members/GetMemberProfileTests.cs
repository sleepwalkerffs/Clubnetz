using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

// Who may see which member is checked in the security tests: the tenant does not reach the handler in these tests.
public class GetMemberProfileTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMemberProfile_ReturnsNameAndPicture()
    {
        var (memberId, user) = Query(ctx => (ctx.TestData().Member1.Id, ctx.TestData().User));

        var result = await SendAsync(new GetMemberProfile(memberId));

        result.MemberId.Should().Be(memberId);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
        result.ProfilePictureUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberProfile_MemberWithPicture_ReturnsItsUrl()
    {
        var (memberId, userId) = await QueryAsync(async ctx =>
        {
            var member = ctx.TestData().Member1;
            ctx.Add(new UserProfilePicture(member.UserId, [1, 2, 3], "image/jpeg"));
            await ctx.SaveChangesAsync();
            return (member.Id, member.UserId);
        });

        var result = await SendAsync(new GetMemberProfile(memberId));

        result.ProfilePictureUrl.Should().Be($"/api/Profile/picture/{userId}");
    }

    [Fact]
    public async Task GetMemberProfile_PlayerOfAnotherClub_ReturnsTheName()
    {
        var memberId = await QueryAsync(async ctx =>
        {
            var club = new Club("ATP Club", new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)), new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)), []);
            ctx.Add(club);
            await ctx.SaveChangesAsync();

            var user = new User("atp@test.com", "atp@test.com", "ATP", "Player", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var member = new ClubMember(user.Id, club.Id, [MemberRole.User]);
            ctx.Add(member);
            await ctx.SaveChangesAsync();
            return member.Id;
        });

        var result = await SendAsync(new GetMemberProfile(memberId));

        result.FirstName.Should().Be("ATP");
        result.LastName.Should().Be("Player");
    }

    [Fact]
    public async Task GetMemberProfile_NonExistentMember_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new GetMemberProfile(int.MaxValue));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
