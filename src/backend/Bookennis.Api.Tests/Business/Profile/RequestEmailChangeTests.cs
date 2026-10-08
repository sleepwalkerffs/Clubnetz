using Bookennis.Api.Business.Account;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class RequestEmailChangeTests
{
    private readonly User user = new("user@bookennis.com", "user@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
    private readonly UserManager<User> userManager = IdentityTestHelper.CreateUserManager();
    private readonly SignInManager<User> signInManager;
    private readonly IMediator mediator = Substitute.For<IMediator>();

    public RequestEmailChangeTests()
    {
        signInManager = IdentityTestHelper.CreateSignInManager(userManager);
        userManager.FindByIdAsync("42").Returns(user);
        signInManager.CheckPasswordSignInAsync(user, "correct", true).Returns(SignInResult.Success);
        signInManager.CheckPasswordSignInAsync(user, "wrong", true).Returns(SignInResult.Failed);
    }

    [Fact]
    public async Task ValidRequest_SendsLinkToNewEmail_WithoutChangingEmail()
    {
        await CreateHandler().Handle(new RequestEmailChange("correct", " new@bookennis.com "), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<SendEmailChangeLink>(c => c.UserId == 42 && c.NewEmail == "new@bookennis.com" && !c.RequestedByAdmin),
            Arg.Any<CancellationToken>());
        user.Email.Should().Be("user@bookennis.com");
    }

    [Fact]
    public async Task WrongPassword_Throws()
    {
        var act = () => CreateHandler().Handle(new RequestEmailChange("wrong", "new@bookennis.com"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(RequestEmailChange.ErrorCode.PasswordMismatch));
        await mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<SendEmailChangeLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockedOut_Throws()
    {
        signInManager.CheckPasswordSignInAsync(user, "correct", true).Returns(SignInResult.LockedOut);

        var act = () => CreateHandler().Handle(new RequestEmailChange("correct", "new@bookennis.com"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(RequestEmailChange.ErrorCode.LockedOut));
    }

    [Fact]
    public async Task SameEmail_Throws()
    {
        var act = () => CreateHandler().Handle(new RequestEmailChange("correct", "USER@bookennis.com"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(RequestEmailChange.ErrorCode.EmailUnchanged));
    }

    [Fact]
    public async Task EmailUsedByOtherUser_Throws()
    {
        userManager.FindByEmailAsync("taken@bookennis.com").Returns(new User("taken@bookennis.com", "taken@bookennis.com", "ta", "ken", new DateOnly(2000, 1, 1), Gender.Male) { Id = 7 });

        var act = () => CreateHandler().Handle(new RequestEmailChange("correct", "taken@bookennis.com"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(RequestEmailChange.ErrorCode.DuplicateEmail));
        await mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<SendEmailChangeLink>(), Arg.Any<CancellationToken>());
    }

    private RequestEmailChange.Handler CreateHandler()
        => new(userManager, signInManager, IdentityTestHelper.CreateUserAccessor(42), mediator);
}
