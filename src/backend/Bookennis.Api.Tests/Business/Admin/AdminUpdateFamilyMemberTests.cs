using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminUpdateFamilyMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminUpdateFamilyMember_UpdatesUserDetails()
    {
        var (clubId, memberId) = Query(ctx =>
        {
            var member = ctx.TestData().Member1;
            return (member.ClubId, member.Id);
        });

        await SendAsync(new AdminUpdateFamilyMember(clubId, memberId, "NewFirst", "NewLast", new DateOnly(2005, 3, 15), Gender.Female));

        var (_, userId) = Query(ctx =>
        {
            var member = ctx.TestData().Member1;
            return (member.Id, member.UserId);
        });

        var user = await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId));
        user.FirstName.Should().Be("NewFirst");
        user.LastName.Should().Be("NewLast");
        user.Birthday.Should().Be(new DateOnly(2005, 3, 15));
        user.Gender.Should().Be(Gender.Female);
    }

    [Fact]
    public async Task AdminUpdateFamilyMember_NonExistentMember_ThrowsEntityNotFoundException()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var act = () => SendAsync(new AdminUpdateFamilyMember(clubId, 999999, "Test", "Test", new DateOnly(2000, 1, 1), Gender.Male));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
