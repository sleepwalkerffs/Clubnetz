using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;
using OrderStatus = Bookennis.Shared.Controller.Memberships.MembershipOrderStatus;

namespace Bookennis.Api.Tests.Business.Memberships;

public class ApproveMembershipOrderTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ApproveMembershipOrder_MakesTheOrderPayableAndTellsTheMember()
    {
        var notifier = GetInstance<IMembershipNotifier>();
        notifier.ClearReceivedCalls();
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OrderAwaitingApproval(ctx));

        var result = await SendAsync(new ApproveMembershipOrder(TestDataSeed.ClubId, orderId));

        result.Status.Should().Be(OrderStatus.AwaitingPayment);
        result.ApprovedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        result.CanPay.Should().BeTrue();
        await notifier.Received(1).Approved(Arg.Is<MembershipOrder>(o => o.Id == orderId), Arg.Any<CancellationToken>());
        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse("the member still has to pay");
    }

    [Fact]
    public async Task ApproveMembershipOrder_OrderThatIsNotWaitingForIt_Throws()
    {
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OpenOrder(ctx));

        var act = () => SendAsync(new ApproveMembershipOrder(TestDataSeed.ClubId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
    }
}
