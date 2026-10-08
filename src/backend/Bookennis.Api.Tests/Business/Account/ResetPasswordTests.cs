using Bookennis.Api.Business.Account;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Account;

public class ResetPasswordTests
{
    private readonly User user = new("user@bookennis.com", "user@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
    private readonly UserManager<User> userManager = IdentityTestHelper.CreateUserManager();

    public ResetPasswordTests() => userManager.FindByEmailAsync("user@bookennis.com").Returns(user);

    [Fact]
    public async Task ValidToken_ResetsPassword_LiftsLockout_AndConfirmsEmail()
    {
        userManager.ResetPasswordAsync(user, "token", "New-Pass1").Returns(IdentityResult.Success);

        await new ResetPassword.Handler(userManager).Handle(new ResetPassword("user@bookennis.com", "token", "New-Pass1"), CancellationToken.None);

        await userManager.Received(1).ResetAccessFailedCountAsync(user);
        await userManager.Received(1).SetLockoutEndDateAsync(user, null);
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidToken_Throws_AndKeepsLockout()
    {
        userManager.ResetPasswordAsync(user, "bad", "New-Pass1").Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        var act = () => new ResetPassword.Handler(userManager).Handle(new ResetPassword("user@bookennis.com", "bad", "New-Pass1"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorDetails.Should().BeEquivalentTo("InvalidToken");
        await userManager.DidNotReceiveWithAnyArgs().SetLockoutEndDateAsync(default!, default);
    }

    [Fact]
    public async Task UnknownEmail_ThrowsLikeAnInvalidToken()
    {
        var act = () => new ResetPassword.Handler(userManager).Handle(new ResetPassword("unknown@bookennis.com", "token", "New-Pass1"), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(ResetPassword.Handler.Error.IdentityError));
        exception.ErrorDetails.Should().BeEquivalentTo("InvalidToken");
    }
}
