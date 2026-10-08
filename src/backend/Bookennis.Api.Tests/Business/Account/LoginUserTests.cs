using Bookennis.Api.Business.Account;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Account;

public class LoginUserTests
{
    private readonly User user = new("user@bookennis.com", "user@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
    private readonly UserManager<User> userManager = IdentityTestHelper.CreateUserManager();
    private readonly SignInManager<User> signInManager;

    public LoginUserTests()
    {
        signInManager = IdentityTestHelper.CreateSignInManager(userManager);
        userManager.FindByEmailAsync("user@bookennis.com").Returns(user);
    }

    [Fact]
    public async Task CorrectPassword_SignsIn_AndCountsTowardsLockout()
    {
        signInManager.CheckPasswordSignInAsync(user, "Secret-123", true).Returns(SignInResult.Success);

        await CreateHandler().Handle(new LoginUser("user@bookennis.com", "Secret-123", true), CancellationToken.None);

        await signInManager.Received(1).SignInAsync(user, Arg.Is<AuthenticationProperties>(p => p.IsPersistent), Arg.Any<string?>());
    }

    [Fact]
    public async Task UnknownEmail_ThrowsInvalidCredentials()
    {
        var act = () => CreateHandler().Handle(new LoginUser("unknown@bookennis.com", "Secret-123", false), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(LoginUser.ErrorCode.InvalidCredentials));
        await signInManager.DidNotReceiveWithAnyArgs().CheckPasswordSignInAsync(default!, default!, default);
    }

    [Fact]
    public async Task WrongPassword_ThrowsSameErrorAsUnknownEmail()
    {
        signInManager.CheckPasswordSignInAsync(user, "wrong", true).Returns(SignInResult.Failed);

        var act = () => CreateHandler().Handle(new LoginUser("user@bookennis.com", "wrong", false), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(LoginUser.ErrorCode.InvalidCredentials));
        await signInManager.DidNotReceiveWithAnyArgs().SignInAsync(default!, default(AuthenticationProperties)!, default);
    }

    [Fact]
    public async Task LockedOut_ThrowsLockedOut()
    {
        signInManager.CheckPasswordSignInAsync(user, "Secret-123", true).Returns(SignInResult.LockedOut);

        var act = () => CreateHandler().Handle(new LoginUser("user@bookennis.com", "Secret-123", false), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(LoginUser.ErrorCode.LockedOut));
    }

    [Fact]
    public async Task UnconfirmedEmail_CorrectPassword_ThrowsNotAllowed()
    {
        signInManager.CheckPasswordSignInAsync(user, "Secret-123", true).Returns(SignInResult.NotAllowed);
        userManager.CheckPasswordAsync(user, "Secret-123").Returns(true);

        var act = () => CreateHandler().Handle(new LoginUser("user@bookennis.com", "Secret-123", false), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(LoginUser.ErrorCode.NotAllowed));
    }

    [Fact]
    public async Task UnconfirmedEmail_WrongPassword_DoesNotRevealTheAccount()
    {
        signInManager.CheckPasswordSignInAsync(user, "wrong", true).Returns(SignInResult.NotAllowed);
        userManager.CheckPasswordAsync(user, "wrong").Returns(false);

        var act = () => CreateHandler().Handle(new LoginUser("user@bookennis.com", "wrong", false), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(LoginUser.ErrorCode.InvalidCredentials));
    }

    private LoginUser.Handler CreateHandler() => new(userManager, signInManager);
}
