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

public class MarkMembershipOrderPaidTests : TestBase
{
    private readonly IMembershipNotifier notifier;
    private readonly IStripeGateway gateway;

    public MarkMembershipOrderPaidTests(TestFixture fixture) : base(fixture)
    {
        notifier = GetInstance<IMembershipNotifier>();
        notifier.ClearReceivedCalls();
        gateway = GetInstance<IStripeGateway>();
        gateway.ClearReceivedCalls();
        gateway.IsConfigured.Returns(true);
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_EnrollsTheMembersAndThanksThePayer()
    {
        var (family, orderId) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.FamilyData());

            // The partner is already enrolled by hand: that enrollment stays as it is
            await MembershipSeed.Enroll(ctx, seeded.SeasonId, seeded.Partner);
            return (seeded, (await MembershipSeed.SeedOrder(ctx, plan, seeded.Parent, seeded.Everybody)).Id);
        });

        var result = await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.BankTransfer, MembershipSeed.Today.AddDays(-3), " Paid on the 3rd "));

        result.Status.Should().Be(OrderStatus.Paid);
        result.PaymentMethod.Should().Be(Bookennis.Shared.Controller.Memberships.MembershipPaymentMethod.BankTransfer);
        result.PaidAt!.Value.UtcDateTime.Date.Should().Be(MembershipSeed.Today.AddDays(-3).ToDateTime(TimeOnly.MinValue));

        var order = await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync());
        order.MarkedPaidByMemberId.Should().Be(TestDataSeed.Member2Id);
        order.Note.Should().Be("Paid on the 3rd");

        var enrollments = await QueryAsync(ctx => ctx.MemberSeasons.Where(ms => ms.SeasonId == family.SeasonId).ToListAsync());
        enrollments.Select(e => e.MemberId).Should().BeEquivalentTo(family.Everybody);
        enrollments.Single(e => e.MemberId == family.Partner).MembershipOrderId.Should().BeNull("enrolled by hand before");
        enrollments.Where(e => e.MemberId != family.Partner).Should().OnlyContain(e => e.MembershipOrderId == orderId);

        await notifier.Received(1).PaymentReceived(Arg.Is<MembershipOrder>(o => o.Id == orderId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_PassivePlan_RecordsThePaymentWithoutEnrolling()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Passive", 3000) with { GrantsSeasonEnrollment = false });
            return (await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id)).Id;
        });

        var result = await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.Cash, default, null));

        result.Status.Should().Be(OrderStatus.Paid);
        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_OnlineAsMethod_IsRecordedAsOther()
    {
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OpenOrder(ctx));

        await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.Online, default, null));

        (await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync())).PaymentMethod.Should().Be(MembershipPaymentMethod.Other, "online payments are only reported by the payment provider");
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_InSupportMode_WorksWithoutAMember()
    {
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OpenOrder(ctx));

        // An administrator of the installation who is no member of the club
        await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, 987654, orderId, MembershipPaymentMethod.Cash, default, null));

        var order = await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync());
        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.MarkedPaidByMemberId.Should().BeNull();
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_OrderWithAnOpenPaymentPage_ClosesIt()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            ctx.Add(new ClubPaymentAccount(TestDataSeed.ClubId, "acct_1", DateTimeOffset.UtcNow));
            var id = await MembershipPaymentSeed.OpenOrder(ctx);
            (await ctx.MembershipOrders.SingleAsync()).StartCheckout("cs_open");
            await ctx.SaveChangesAsync();
            return id;
        });

        await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.Cash, default, null));

        await gateway.Received(1).ExpireCheckoutSession("acct_1", "cs_open", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_AlreadyPaid_ChangesNothingAndSendsNothing()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var id = await MembershipPaymentSeed.OpenOrder(ctx);
            (await ctx.MembershipOrders.SingleAsync()).MarkPaid(MembershipPaymentMethod.Online, DateTimeOffset.UtcNow.AddDays(-1));
            await ctx.SaveChangesAsync();
            return id;
        });

        await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.Cash, default, null));

        (await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync())).PaymentMethod.Should().Be(MembershipPaymentMethod.Online);
        await notifier.DidNotReceiveWithAnyArgs().PaymentReceived(default!, default);
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_CancelledOrder_Throws()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var id = await MembershipPaymentSeed.OpenOrder(ctx);
            (await ctx.MembershipOrders.SingleAsync()).CancelByMember();
            await ctx.SaveChangesAsync();
            return id;
        });

        var act = () => SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.Cash, default, null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
    }
}
