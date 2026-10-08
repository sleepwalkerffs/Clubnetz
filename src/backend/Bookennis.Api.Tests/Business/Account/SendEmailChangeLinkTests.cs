using Bookennis.Api.Business.Account;
using Bookennis.Api.Business.Admin;
using Bookennis.Api.Config;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using Fusonic.Extensions.Email;
using Fusonic.Extensions.Mediator;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Account;

public class SendEmailChangeLinkTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendsConfirmationLinkToNewEmail(bool requestedByAdmin)
    {
        var user = new User("old@bookennis.com", "old@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male) { Id = 42 };
        var userManager = IdentityTestHelper.CreateUserManager();
        userManager.FindByIdAsync("42").Returns(user);
        userManager.GenerateChangeEmailTokenAsync(user, "new@bookennis.com").Returns("the-token");
        var mediator = Substitute.For<IMediator>();

        var handler = new SendEmailChangeLink.Handler(userManager, mediator, new AppSettings { AppUrl = "https://bookennis.test" });
        await handler.Handle(new SendEmailChangeLink(42, "new@bookennis.com", requestedByAdmin), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<SendEmail>(e => e.Recipient == "new@bookennis.com"
                && e.ViewModel is ConfirmEmailChangeViewModel
                && ((ConfirmEmailChangeViewModel)e.ViewModel).RequestedByAdmin == requestedByAdmin
                && ((ConfirmEmailChangeViewModel)e.ViewModel).Url.StartsWith("https://bookennis.test/Account/ConfirmEmailChange?userId=42")
                && ((ConfirmEmailChangeViewModel)e.ViewModel).Url.Contains("token=the-token")),
            Arg.Any<CancellationToken>());
    }
}
