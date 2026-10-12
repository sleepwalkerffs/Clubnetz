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

public class RejectMembershipOrderTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task RejectMembershipOrder_CancelsTheOrderSoTheMemberCanChooseAnotherPlan()
    {
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OrderAwaitingApproval(ctx));

        var result = await SendAsync(new RejectMembershipOrder(TestDataSeed.ClubId, orderId));

        result.Status.Should().Be(OrderStatus.Cancelled);

        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, result.SeasonId, MembershipSeed.Data())).Id);
        var next = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));
        next.Status.Should().Be(OrderStatus.AwaitingPayment);
    }

    [Fact]
    public async Task RejectMembershipOrder_OrderThatIsNotWaitingForIt_Throws()
    {
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OpenOrder(ctx));

        var act = () => SendAsync(new RejectMembershipOrder(TestDataSeed.ClubId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
    }
}
