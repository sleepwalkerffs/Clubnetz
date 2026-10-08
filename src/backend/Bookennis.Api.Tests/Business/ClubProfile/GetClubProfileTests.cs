using Bookennis.Api.Business.ClubProfile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubProfile;

public class GetClubProfileTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetClubProfile_ReturnsClubProfileForUser()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        var result = await SendAsync(new GetClubProfile(userId, false));

        result.ClubName.Should().Be("TestClub");
        result.MemberId.Should().BeGreaterThan(0);
        result.Role.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetClubProfile_DualMembership_NormalSession_ReturnsClubMember()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var td = ctx.TestData();

            // Add a GuestMember for the same user+club to create dual membership
            var guestMember = new GuestMember(td.User.Id, td.Club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            return td.User.Id;
        });

        var result = await SendAsync(new GetClubProfile(userId, false));

        result.ClubName.Should().Be("TestClub");
        result.Role.Should().NotContain(Bookennis.Shared.Controller.Shared.MemberRole.Guest);
    }

    [Fact]
    public async Task GetClubProfile_DualMembership_GuestSession_ReturnsGuestMember()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var td = ctx.TestData();

            // Add a GuestMember for the same user+club to create dual membership
            var guestMember = new GuestMember(td.User.Id, td.Club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            return td.User.Id;
        });

        var result = await SendAsync(new GetClubProfile(userId, true));

        result.ClubName.Should().Be("TestClub");
        result.Role.Should().Contain(Bookennis.Shared.Controller.Shared.MemberRole.Guest);
    }
}
