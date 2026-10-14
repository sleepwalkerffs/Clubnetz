using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Tests.Business.Payments;

public class ConnectPaymentAccountTests(TestFixture fixture) : PaymentTestBase(fixture)
{
    [Fact]
    public async Task ConnectPaymentAccount_FirstTime_CreatesTheAccountAndReturnsTheOnboardingLink()
    {
        Gateway.CreateAccount(Arg.Any<StripeAccountData>(), Arg.Any<CancellationToken>()).Returns(AccountId);
        Gateway.CreateAccountLink(AccountId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("https://connect.stripe.test/onboarding");

        var result = await SendAsync(new ConnectPaymentAccount(TestDataSeed.ClubId, "DE"));

        result.Url.Should().Be("https://connect.stripe.test/onboarding");
        await Gateway.Received(1).CreateAccount(Arg.Is<StripeAccountData>(d => d.ClubId == TestDataSeed.ClubId && d.ClubName == "TestClub" && d.Country == "DE"), Arg.Any<CancellationToken>());
        await Gateway.Received(1).CreateAccountLink(
            AccountId,
            $"http://notvalid.com/clubs/{TestDataSeed.ClubId}/membership-plans?stripe=refresh",
            $"http://notvalid.com/clubs/{TestDataSeed.ClubId}/membership-plans?stripe=return",
            Arg.Any<CancellationToken>());

        var account = await QueryAsync(ctx => ctx.ClubPaymentAccounts.SingleAsync());
        account.Should().BeEquivalentTo(new { ClubId = TestDataSeed.ClubId, StripeAccountId = AccountId, ChargesEnabled = false, DetailsSubmitted = false }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task ConnectPaymentAccount_AgainBeforeTheOnboardingIsDone_ContinuesWithTheSameAccount()
    {
        await SeedAccount(chargesEnabled: false);
        Gateway.CreateAccountLink(AccountId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("https://connect.stripe.test/continue");

        var result = await SendAsync(new ConnectPaymentAccount(TestDataSeed.ClubId, "AT"));

        result.Url.Should().Be("https://connect.stripe.test/continue");
        await Gateway.DidNotReceiveWithAnyArgs().CreateAccount(default!, default);
        (await QueryAsync(ctx => ctx.ClubPaymentAccounts.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task ConnectPaymentAccount_InstallationWithoutKeys_Throws()
    {
        Gateway.IsConfigured.Returns(false);

        var act = () => SendAsync(new ConnectPaymentAccount(TestDataSeed.ClubId, "AT"));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PaymentErrorCode.PaymentsNotConfigured));
        await Gateway.DidNotReceiveWithAnyArgs().CreateAccount(default!, default);
    }

    [Fact]
    public async Task ConnectPaymentAccount_StripeDeclines_ThrowsALocalizedErrorAndStoresNothing()
    {
        Gateway.CreateAccount(Arg.Any<StripeAccountData>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new global::Stripe.StripeException("Connect is not enabled"));

        var act = () => SendAsync(new ConnectPaymentAccount(TestDataSeed.ClubId, "AT"));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PaymentErrorCode.PaymentProviderUnavailable));
        (await QueryAsync(ctx => ctx.ClubPaymentAccounts.AnyAsync())).Should().BeFalse();
    }
}
