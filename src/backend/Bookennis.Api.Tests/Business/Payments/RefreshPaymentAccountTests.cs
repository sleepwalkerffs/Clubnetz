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

public class RefreshPaymentAccountTests(TestFixture fixture) : PaymentTestBase(fixture)
{
    [Fact]
    public async Task RefreshPaymentAccount_TakesOverTheStatusOfStripe()
    {
        await SeedAccount(chargesEnabled: false);
        Gateway.GetAccount(AccountId, Arg.Any<CancellationToken>()).Returns(new StripeAccountInfo(AccountId, ChargesEnabled: true, PayoutsEnabled: false, DetailsSubmitted: true));

        var result = await SendAsync(new RefreshPaymentAccount(TestDataSeed.ClubId));

        result.Should().BeEquivalentTo(new { IsConnected = true, ChargesEnabled = true, PayoutsEnabled = false, DetailsSubmitted = true }, o => o.ExcludingMissingMembers());
        (await QueryAsync(ctx => ctx.ClubPaymentAccounts.SingleAsync())).ChargesEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshPaymentAccount_AccountIsGoneAtStripe_DisconnectsTheClub()
    {
        await SeedAccount();
        Gateway.GetAccount(AccountId, Arg.Any<CancellationToken>()).Returns((StripeAccountInfo?)null);

        var result = await SendAsync(new RefreshPaymentAccount(TestDataSeed.ClubId));

        result.IsConnected.Should().BeFalse();
        (await QueryAsync(ctx => ctx.ClubPaymentAccounts.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task RefreshPaymentAccount_NotConnected_AsksNothing()
    {
        var result = await SendAsync(new RefreshPaymentAccount(TestDataSeed.ClubId));

        result.IsConnected.Should().BeFalse();
        await Gateway.DidNotReceiveWithAnyArgs().GetAccount(default!, default);
    }
}
