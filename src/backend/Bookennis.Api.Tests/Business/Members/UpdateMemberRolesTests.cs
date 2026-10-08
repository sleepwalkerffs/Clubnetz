using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class UpdateMemberRolesTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateMemberRoles_ValidRoles_UpdatesSuccessfully()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        await SendAsync(new UpdateMemberRoles(memberId, [MemberRole.Admin, MemberRole.Trainer]));

        var member = await QueryAsync(ctx => ctx.ClubMembers.SingleAsync(m => m.Id == memberId));
        member.UserRoles.Should().BeEquivalentTo([MemberRole.Admin, MemberRole.Trainer]);
    }

    [Fact]
    public async Task UpdateMemberRoles_SingleRole_UpdatesSuccessfully()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        await SendAsync(new UpdateMemberRoles(memberId, [MemberRole.Maintainer]));

        var member = await QueryAsync(ctx => ctx.ClubMembers.SingleAsync(m => m.Id == memberId));
        member.UserRoles.Should().BeEquivalentTo([MemberRole.Maintainer]);
    }

    [Fact]
    public async Task UpdateMemberRoles_NonExistentMember_ThrowsException()
    {
        var act = () => SendAsync(new UpdateMemberRoles(999999, [MemberRole.User]));

        await act.Should().ThrowAsync<Exception>();
    }
}
