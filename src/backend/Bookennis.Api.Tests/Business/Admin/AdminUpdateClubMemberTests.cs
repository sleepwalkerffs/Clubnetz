using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminUpdateClubMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminUpdateClubMember_UpdatesMemberRolesAndOptions()
    {
        var (clubId, memberId) = Query(ctx =>
        {
            var member = ctx.TestData().Member1;
            return (member.ClubId, member.Id);
        });

        await SendAsync(new AdminUpdateClubMember(clubId, memberId, [MemberRole.Admin], AllowedSeasonIds: [], BookingsPerWeek: 10));

        var member = await QueryAsync(ctx => ctx.ClubMembers.SingleAsync(m => m.Id == memberId));
        member.UserRoles.Should().Contain(MemberRole.Admin);
        member.BookingsPerWeek.Should().Be(10);
    }
}
