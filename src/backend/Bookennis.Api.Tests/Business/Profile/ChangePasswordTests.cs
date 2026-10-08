using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class ChangePasswordTests
{
    private readonly User user = new("user@bookennis.com", "user@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
    private readonly UserManager<User> userManager = IdentityTestHelper.CreateUserManager();
    private readonly SignInManager<User> signInManager;

    public ChangePasswordTests()
    {
        signInManager = IdentityTestHelper.CreateSignInManager(userManager);
        userManager.FindByIdAsync("42").Returns(user);
    }

    [Fact]
    public async Task ValidRequest_ChangesPassword_AndRefreshesSignIn()
    {
        userManager.ChangePasswordAsync(user, "Old-Pass1", "New-Pass1").Returns(IdentityResult.Success);

        await CreateHandler().Handle(new ChangePassword("Old-Pass1", "New-Pass1"), CancellationToken.None);

        await userManager.Received(1).ChangePasswordAsync(user, "Old-Pass1", "New-Pass1");
        await signInManager.Received(1).RefreshSignInAsync(user);
    }

    [Fact]
    public async Task WrongCurrentPassword_ThrowsIdentityError()
    {
        userManager.ChangePasswordAsync(user, "wrong", "New-Pass1")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch" }));

        var act = () => CreateHandler().Handle(new ChangePassword("wrong", "New-Pass1"), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be("IdentityError");
        exception.ErrorDetails.Should().BeEquivalentTo("PasswordMismatch");
        await signInManager.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
    }

    private ChangePassword.Handler CreateHandler()
        => new(userManager, signInManager, IdentityTestHelper.CreateUserAccessor(42));
}
