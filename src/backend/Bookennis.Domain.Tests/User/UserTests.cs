using Bookennis.Domain.User;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.User;

public class UserTests
{
    [Fact]
    public void TransferTo_ChildUser_ChangesTheOwner()
    {
        var child = new Domain.User.User("Kid", "Child", new DateOnly(2015, 1, 1), Gender.Male, belongsToUserId: 1);

        child.TransferTo(2);

        child.BelongsToUserId.Should().Be(2);
    }

    [Fact]
    public void TransferTo_UserWithOwnLogin_Throws()
    {
        var user = new Domain.User.User("user@bookennis.com", "user@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male);

        var act = () => user.TransferTo(2);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AcceptPrivacyPolicy_SetsTheTimestamp()
    {
        var user = new Domain.User.User("user@bookennis.com", "user@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male);

        user.AcceptPrivacyPolicy();

        user.PrivacyPolicyAcceptedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }
}
