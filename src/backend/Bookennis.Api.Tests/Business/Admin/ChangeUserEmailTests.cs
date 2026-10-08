using Bookennis.Api.Business.Account;
using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class ChangeUserEmailTests
{
    private readonly User user = new("old@bookennis.com", "old@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> userManager = IdentityTestHelper.CreateUserManager();
    private readonly IMediator mediator = Substitute.For<IMediator>();

    public ChangeUserEmailTests() => userManager.FindByIdAsync("42").Returns(user);

    [Fact]
    public async Task ValidRequest_SendsAdminLink()
    {
        await new ChangeUserEmail.Handler(userManager, mediator).Handle(new ChangeUserEmail(42, "new@bookennis.com"), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<SendEmailChangeLink>(c => c.UserId == 42 && c.NewEmail == "new@bookennis.com" && c.RequestedByAdmin),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailInUse_Throws()
    {
        userManager.FindByEmailAsync("taken@bookennis.com").Returns(new User("taken@bookennis.com", "taken@bookennis.com", "ta", "ken", new DateOnly(2000, 1, 1), Gender.Male) { Id = 7 });

        var act = () => new ChangeUserEmail.Handler(userManager, mediator).Handle(new ChangeUserEmail(42, "taken@bookennis.com"), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(ChangeUserEmail.ErrorCode.EmailAlreadyInUse));
    }
}
