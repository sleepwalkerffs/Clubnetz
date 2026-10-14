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

public class GetPaymentConfigurationTests(TestFixture fixture) : PaymentTestBase(fixture)
{
    [Fact]
    public async Task GetPaymentConfiguration_InstallationWithoutKeys_NothingIsAvailable()
    {
        Gateway.IsConfigured.Returns(false);
        await SeedAccount();

        var result = await SendAsync(new GetPaymentConfiguration(TestDataSeed.ClubId));

        result.Should().BeEquivalentTo(new PaymentConfigurationResult { IsAvailable = false, IsConnected = true, CanAcceptPayments = false });
    }

    [Fact]
    public async Task GetPaymentConfiguration_ClubNotConnected()
    {
        var result = await SendAsync(new GetPaymentConfiguration(TestDataSeed.ClubId));

        result.Should().BeEquivalentTo(new PaymentConfigurationResult { IsAvailable = true, IsConnected = false, CanAcceptPayments = false });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetPaymentConfiguration_ConnectedClub_TakesPaymentsOnceTheOnboardingIsDone(bool chargesEnabled)
    {
        await SeedAccount(chargesEnabled);

        var result = await SendAsync(new GetPaymentConfiguration(TestDataSeed.ClubId));

        result.Should().BeEquivalentTo(new PaymentConfigurationResult { IsAvailable = true, IsConnected = true, CanAcceptPayments = chargesEnabled });
    }
}
