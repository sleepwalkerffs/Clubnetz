using Bookennis.Api.Business.Admin;
using Bookennis.Api.Config;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Email;
using Fusonic.Extensions.Mediator;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class ConfirmEmailChangeTests
{
    private readonly User user = new("old@bookennis.com", "old@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
    private readonly UserManager<User> userManager = IdentityTestHelper.CreateUserManager();
    private readonly IMediator mediator = Substitute.For<IMediator>();

    public ConfirmEmailChangeTests()
    {
        userManager.FindByIdAsync("42").Returns(user);
        userManager.ChangeEmailAsync(user, "new@bookennis.com", "token").Returns(_ =>
        {
            user.Email = "new@bookennis.com";
            return IdentityResult.Success;
        });
        userManager.ChangeEmailAsync(user, "new@bookennis.com", "bad").Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));
    }

    [Fact]
    public async Task ValidToken_UpdatesUserName_AndNotifiesOldEmail()
    {
        await CreateHandler().Handle(new ConfirmEmailChange(42, "new@bookennis.com", "token"), CancellationToken.None);

        await userManager.Received(1).SetUserNameAsync(user, "new@bookennis.com");
        await mediator.Received(1).Send(
            Arg.Is<SendEmail>(e => e.Recipient == "old@bookennis.com"
                && e.ViewModel is EmailChangedViewModel
                && ((EmailChangedViewModel)e.ViewModel).NewEmail == "new@bookennis.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserNameDiffersFromEmail_KeepsUserName()
    {
        user.UserName = "custom-name";

        await CreateHandler().Handle(new ConfirmEmailChange(42, "new@bookennis.com", "token"), CancellationToken.None);

        await userManager.DidNotReceiveWithAnyArgs().SetUserNameAsync(default!, default);
    }

    [Fact]
    public async Task AlreadyChanged_IsNoOp()
    {
        user.Email = "new@bookennis.com";
        user.EmailConfirmed = true;

        await CreateHandler().Handle(new ConfirmEmailChange(42, "new@bookennis.com", "used-token"), CancellationToken.None);

        await userManager.DidNotReceiveWithAnyArgs().ChangeEmailAsync(default!, default!, default!);
        await mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<SendEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidToken_Throws_AndSendsNothing()
    {
        var act = () => CreateHandler().Handle(new ConfirmEmailChange(42, "new@bookennis.com", "bad"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(ConfirmEmailChange.ErrorCode.InvalidToken));
        await mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<SendEmail>(), Arg.Any<CancellationToken>());
    }

    private ConfirmEmailChange.Handler CreateHandler()
        => new(userManager, mediator, new AppSettings { AppUrl = "https://bookennis.test" });
}
