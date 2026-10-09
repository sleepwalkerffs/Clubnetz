using Bookennis.Api.Business.ClubProfile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
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
    public async Task GetClubProfile_Member_IsNotInSupportMode()
    {
        var (userId, clubId) = Query(ctx => (ctx.TestData().User.Id, ctx.TestData().Club.Id));

        // An application administrator who is a member of the club gets the profile of the member
        var result = await SendAsync(new GetClubProfile(userId, false, clubId));

        result.IsSupportMode.Should().BeFalse();
        result.MemberId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetClubProfile_AdministratorWithoutMember_ReturnsSupportProfile()
    {
        var (userId, clubId) = await AddUserWithoutMember();

        var result = await SendAsync(new GetClubProfile(userId, false, clubId));

        result.IsSupportMode.Should().BeTrue();
        result.ClubName.Should().Be("TestClub");
        result.MemberId.Should().Be(0);
        result.Role.Should().BeEquivalentTo([Bookennis.Shared.Controller.Shared.MemberRole.Admin]);
    }

    [Fact]
    public async Task GetClubProfile_UserWithoutMember_Throws()
    {
        var (userId, _) = await AddUserWithoutMember();

        var act = () => SendAsync(new GetClubProfile(userId, false));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }

    [Fact]
    public async Task GetClubProfile_SupportMode_UnknownClub_Throws()
    {
        var (userId, _) = await AddUserWithoutMember();

        var act = () => SendAsync(new GetClubProfile(userId, false, 999999));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }

    private Task<(int UserId, int ClubId)> AddUserWithoutMember()
        => QueryAsync(async ctx =>
        {
            var user = new User("support@bookennis.com", "support@bookennis.com", "Sup", "Port", new DateOnly(1990, 1, 1), Gender.Male, "Support St 1", "Vienna", "1010", Country.Austria);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            return (user.Id, ctx.TestData().Club.Id);
        });

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
