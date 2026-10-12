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

public class GetPaymentAccountTests(TestFixture fixture) : PaymentTestBase(fixture)
{
    [Fact]
    public async Task GetPaymentAccount_NotConnected()
    {
        var result = await SendAsync(new GetPaymentAccount(TestDataSeed.ClubId));

        result.Should().BeEquivalentTo(new PaymentAccountDto { IsAvailable = true, IsConnected = false, ChargesEnabled = false, PayoutsEnabled = false, DetailsSubmitted = false, ConnectedAt = null });
    }

    [Fact]
    public async Task GetPaymentAccount_Connected_ReturnsTheStatusButNotTheStripeAccountId()
    {
        await SeedAccount();

        var result = await SendAsync(new GetPaymentAccount(TestDataSeed.ClubId));

        result.Should().BeEquivalentTo(new { IsAvailable = true, IsConnected = true, ChargesEnabled = true, PayoutsEnabled = true, DetailsSubmitted = true }, o => o.ExcludingMissingMembers());
        result.ConnectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        System.Text.Json.JsonSerializer.Serialize(result).Should().NotContain(AccountId);
    }
}
