using Bookennis.Api.Business.Account;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Account;

public class ConfirmEmailTests
{
    private readonly User user = new("new@bookennis.com", "new@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
    private readonly UserManager<User> userManager = IdentityTestHelper.CreateUserManager();

    public ConfirmEmailTests() => userManager.FindByEmailAsync("new@bookennis.com").Returns(user);

    [Fact]
    public async Task ValidToken_ConfirmsEmail()
    {
        userManager.ConfirmEmailAsync(user, "token").Returns(IdentityResult.Success);

        await new ConfirmEmail.Handler(userManager).Handle(new ConfirmEmail("new@bookennis.com", "token"), CancellationToken.None);

        await userManager.Received(1).ConfirmEmailAsync(user, "token");
    }

    [Fact]
    public async Task AlreadyConfirmed_IsNoOp()
    {
        user.EmailConfirmed = true;

        await new ConfirmEmail.Handler(userManager).Handle(new ConfirmEmail("new@bookennis.com", "used-token"), CancellationToken.None);

        await userManager.DidNotReceiveWithAnyArgs().ConfirmEmailAsync(default!, default!);
    }

    [Fact]
    public async Task InvalidToken_Throws()
    {
        userManager.ConfirmEmailAsync(user, "bad").Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        var act = () => new ConfirmEmail.Handler(userManager).Handle(new ConfirmEmail("new@bookennis.com", "bad"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(ConfirmEmail.Handler.ErrorCode.TokenInvalid));
    }

    [Fact]
    public async Task UnknownEmail_ThrowsLikeAnInvalidToken()
    {
        var act = () => new ConfirmEmail.Handler(userManager).Handle(new ConfirmEmail("unknown@bookennis.com", "token"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(ConfirmEmail.Handler.ErrorCode.TokenInvalid));
    }
}
