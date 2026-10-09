using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminUserTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminUser_ReturnsUserDetails()
    {
        var user = Query(ctx => ctx.TestData().User);

        var result = await SendAsync(new GetAdminUser(user.Id));

        result.Id.Should().Be(user.Id);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
        result.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetAdminUser_ReturnsTheClubsOfTheUser()
    {
        var (userId, clubId, memberId) = await QueryAsync(async ctx =>
        {
            var td = ctx.TestData();

            // The same user as guest of the club: both memberships are listed
            ctx.Add(new GuestMember(td.User.Id, td.Club.Id));
            await ctx.SaveChangesAsync();

            return (td.User.Id, td.Club.Id, td.Member1.Id);
        });
        SetTenantId(0);

        var result = await SendAsync(new GetAdminUser(userId));

        result.Clubs.Should().HaveCount(2);
        result.Clubs.Should().OnlyContain(c => c.ClubId == clubId && c.ClubName == "TestClub");
        var clubMember = result.Clubs.Single(c => !c.IsGuest);
        clubMember.MemberId.Should().Be(memberId);
        clubMember.Roles.Should().BeEquivalentTo([Bookennis.Shared.Controller.Shared.MemberRole.User]);
        result.Clubs.Should().ContainSingle(c => c.IsGuest);
    }

    [Fact]
    public async Task GetAdminUser_NonExistentUser_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new GetAdminUser(999999));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
