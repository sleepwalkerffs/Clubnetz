using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class GetMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMember_ReturnsCorrectMemberDetails()
    {
        var (memberId, user) = Query(ctx =>
        {
            var member = ctx.TestData().Member1;
            var u = ctx.TestData().User;
            return (member.Id, u);
        });

        var result = await SendAsync(new GetMember(memberId));

        result.MemberId.Should().Be(memberId);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
    }

    [Fact]
    public async Task GetMember_AtpPlayerFromDifferentClub_ReturnsCorrectMemberDetails()
    {
        // Arrange: create an ATP club with a member (simulating a cross-club ATP player)
        var (atpMemberId, atpUserId) = await ScopedAsync(async () =>
        {
            var ctx = GetInstance<Bookennis.Api.Data.AppDbContext>();
            var atpClub = new Club(
                name: "ATP Club",
                openingHours: new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
                primeTimeHours: new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
                playModes: []
            );
            ctx.Clubs.Add(atpClub);
            await ctx.SaveChangesAsync();

            var atpUser = new Bookennis.Domain.User.User("atp@test.com", "atp@test.com", "ATP", "Player",
                new DateOnly(1990, 1, 1), Bookennis.Domain.User.Gender.Male, "Street 1", "Vienna", "1010", Bookennis.Domain.User.Country.Austria);
            ctx.Users.Add(atpUser);
            await ctx.SaveChangesAsync();

            var atpMember = new ClubMember(atpUser.Id, atpClub.Id, [MemberRole.User]);
            ctx.ClubMembers.Add(atpMember);
            await ctx.SaveChangesAsync();

            return (atpMember.Id, atpUser.Id);
        });

        // Tenant is set to the main club, but the ATP member belongs to a different club
        var result = await SendAsync(new GetMember(atpMemberId));

        result.MemberId.Should().Be(atpMemberId);
        result.FirstName.Should().Be("ATP");
        result.LastName.Should().Be("Player");
    }

    [Fact]
    public async Task GetMember_NonExistentMember_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new GetMember(int.MaxValue));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
