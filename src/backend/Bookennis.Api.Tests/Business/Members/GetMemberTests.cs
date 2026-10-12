using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
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
    public async Task GetMember_NonExistentMember_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new GetMember(int.MaxValue));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
