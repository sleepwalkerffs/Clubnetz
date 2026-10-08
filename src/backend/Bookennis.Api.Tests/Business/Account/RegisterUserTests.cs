using Bookennis.Api.Business.Account;
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

namespace Bookennis.Api.Tests.Business.Account;

public class RegisterUserTests
{
    private readonly UserManager<User> userManager = IdentityTestHelper.CreateUserManager();
    private readonly IMediator mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task WithoutAddress_CreatesUser_AssignsRole_AndSendsVerificationEmail()
    {
        User? created = null;
        userManager.CreateAsync(Arg.Do<User>(u => created = u), "Secret-123").Returns(IdentityResult.Success);
        userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<User>()).Returns("token");

        await CreateHandler().Handle(NewCommand(street: null, city: null, zipCode: null), CancellationToken.None);

        created.Should().NotBeNull();
        created!.PrivacyPolicyAcceptedAt.Should().NotBeNull();
        created.Street.Should().BeEmpty();
        created.City.Should().BeEmpty();
        created.ZipCode.Should().BeEmpty();
        await userManager.Received(1).AddToRoleAsync(created, UserRoles.User.ToString());
        await mediator.Received(1).Send(Arg.Is<SendEmail>(e => e.Recipient == "new@bookennis.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithAddress_StoresAddress()
    {
        User? created = null;
        userManager.CreateAsync(Arg.Do<User>(u => created = u), "Secret-123").Returns(IdentityResult.Success);

        await CreateHandler().Handle(NewCommand(street: "Main St 1", city: "Vienna", zipCode: "1010"), CancellationToken.None);

        created!.Street.Should().Be("Main St 1");
        created.City.Should().Be("Vienna");
        created.ZipCode.Should().Be("1010");
    }

    [Fact]
    public async Task CreateFails_DoesNotAssignRole_AndThrows()
    {
        userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail" }));

        var act = () => CreateHandler().Handle(NewCommand(null, null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorDetails.Should().BeEquivalentTo("DuplicateEmail");
        await userManager.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
        await mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<SendEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrivacyPolicyNotAccepted_Throws_AndCreatesNoUser()
    {
        var act = () => CreateHandler().Handle(NewCommand(null, null, null, acceptPrivacyPolicy: false), CancellationToken.None);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(RegisterUser.ErrorCode.PrivacyPolicyNotAccepted));
        await userManager.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!);
    }

    private static RegisterUser NewCommand(string? street, string? city, string? zipCode, bool acceptPrivacyPolicy = true)
        => new("New", "User", new DateOnly(2000, 1, 1), "new@bookennis.com", "new@bookennis.com", "Secret-123", Gender.Male, street, city, zipCode, Country.Austria, acceptPrivacyPolicy);

    private RegisterUser.Handler CreateHandler()
        => new(userManager, mediator, new AppSettings { AppUrl = "https://bookennis.test" });
}
