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

public abstract class PaymentTestBase : TestBase
{
    protected const string AccountId = "acct_club";

    protected PaymentTestBase(TestFixture fixture) : base(fixture)
    {
        Gateway = GetInstance<IStripeGateway>();
        Gateway.ClearSubstitute(ClearOptions.All);
        Gateway.IsConfigured.Returns(true);
    }

    protected IStripeGateway Gateway { get; }

    protected Task SeedAccount(bool chargesEnabled = true, string accountId = AccountId, int clubId = TestDataSeed.ClubId) => QueryAsync(async ctx =>
    {
        var account = new ClubPaymentAccount(clubId, accountId, DateTimeOffset.UtcNow);
        account.UpdateStatus(chargesEnabled, payoutsEnabled: chargesEnabled, detailsSubmitted: chargesEnabled);
        ctx.Add(account);
        await ctx.SaveChangesAsync();
    });
}
