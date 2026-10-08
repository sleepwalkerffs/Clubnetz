using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminRemoveClubMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminRemoveClubMember_RemovesMember()
    {
        // Create a member to remove
        var (clubId, memberId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var newUser = new Domain.User.User("removeme@test.com", "removeme@test.com", "Remove", "Me", new DateOnly(1990, 1, 1), Bookennis.Domain.User.Gender.Male);
            ctx.Add(newUser);
            await ctx.SaveChangesAsync();
            var member = new ClubMember(newUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(member);
            await ctx.SaveChangesAsync();
            return (club.Id, member.Id);
        });

        SetTenantId(0);

        await SendAsync(new AdminRemoveClubMember(clubId, memberId));

        var exists = await QueryAsync(ctx => ctx.ClubMembers.AnyAsync(m => m.Id == memberId));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AdminRemoveClubMember_NonExistentMember_ThrowsEntityNotFoundException()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var act = () => SendAsync(new AdminRemoveClubMember(clubId, 999999));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
