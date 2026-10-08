using System.Security.Claims;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class BecomeClubMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task BecomeClubMember_NewMembership_CreatesMember()
    {
        // Create a new user without a club membership
        var (userId, newClubId) = await QueryAsync(async ctx =>
        {
            var user = new User("newuser@test.com", "newuser@test.com", "New", "User", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            // Create a second club (with no tenant filter since we ignore query filters for the test)
            var club = new Domain.Clubs.Club(
                "SecondClub",
                new Global.Intervals.TimeOnlyInterval(new TimeOnly(08, 00), new TimeOnly(20, 00)),
                new Global.Intervals.TimeOnlyInterval(new TimeOnly(16, 00), new TimeOnly(19, 00)),
                [new Domain.Clubs.Club.PlayModeDto([MemberRole.User], System.Drawing.Color.Blue, 2, false, "Default", TimeSpan.FromHours(1), false, false)]);
            ctx.Add(club);
            await ctx.SaveChangesAsync();
            return (user.Id, club.Id);
        });

        var userAccessor = GetInstance<IUserAccessor>();
        userAccessor.TryGetUser(user: out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });

        SetTenantId(newClubId);

        await SendAsync(new BecomeClubMember(newClubId));

        var hasMembership = await QueryAsync(ctx => ctx.ClubMembers.AnyAsync(m => m.UserId == userId && m.ClubId == newClubId));
        hasMembership.Should().BeTrue();
    }

    [Fact]
    public async Task BecomeClubMember_AlreadyMember_DoesNotDuplicate()
    {
        var (userId, clubId) = Query(ctx =>
        {
            var member = ctx.TestData().Member1;
            return (member.UserId, member.ClubId);
        });

        var userAccessor = GetInstance<IUserAccessor>();
        userAccessor.TryGetUser(out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });

        await SendAsync(new BecomeClubMember(clubId));

        var count = await QueryAsync(ctx => ctx.ClubMembers.CountAsync(m => m.UserId == userId && m.ClubId == clubId));
        count.Should().Be(1);
    }
}
