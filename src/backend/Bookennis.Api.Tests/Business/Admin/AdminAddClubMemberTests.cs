using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminAddClubMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminAddClubMember_AddsNewMember()
    {
        var (clubId, userId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var newUser = new Domain.User.User("newmember@test.com", "newmember@test.com", "New", "Member", new DateOnly(1990, 1, 1), Bookennis.Domain.User.Gender.Male);
            ctx.Add(newUser);
            await ctx.SaveChangesAsync();
            return (club.Id, newUser.Id);
        });

        SetTenantId(0);

        await SendAsync(new AdminAddClubMember(clubId, userId, [MemberRole.User]));

        var exists = await QueryAsync(ctx => ctx.ClubMembers.AnyAsync(m => m.UserId == userId && m.ClubId == clubId));
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AdminAddClubMember_AlreadyMember_DoesNotDuplicate()
    {
        var (clubId, userId) = Query(ctx =>
        {
            var member = ctx.TestData().Member1;
            return (member.ClubId, member.UserId);
        });

        SetTenantId(0);

        await SendAsync(new AdminAddClubMember(clubId, userId, [MemberRole.User]));

        var count = await QueryAsync(ctx => ctx.ClubMembers.CountAsync(m => m.UserId == userId && m.ClubId == clubId));
        count.Should().Be(1);
    }

    [Fact]
    public async Task AdminAddClubMember_NonExistentUser_ThrowsEntityNotFoundException()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var act = () => SendAsync(new AdminAddClubMember(clubId, 999999, [MemberRole.User]));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
