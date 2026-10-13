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
    public async Task ApproveMembershipOrder_WithMembers_TheOrderCoversThemFromNowOn()
    {
        // The member asked for the family plan alone, the treasurer puts the family on it
        var (orderId, family) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Family", 32000) with { MaxAdults = 2, IncludesChildren = true, RequiresApproval = true });
            return ((await MembershipSeed.SeedOrder(ctx, plan, seeded.Parent, seeded.Parent)).Id, seeded);
        });

        var result = await SendAsync(new ApproveMembershipOrder(TestDataSeed.ClubId, orderId, [family.Parent, family.Partner, family.Kid]));

        result.Status.Should().Be(OrderStatus.AwaitingPayment);
        var covered = await QueryAsync(ctx => ctx.MembershipOrderMembers.Where(m => m.MembershipOrderId == orderId).Select(m => m.MemberId).ToListAsync());
        covered.Should().BeEquivalentTo([family.Parent, family.Partner, family.Kid]);
        (await QueryAsync(ctx => ctx.MembershipOrders.Where(o => o.Id == orderId).Select(o => o.PayerMemberId).SingleAsync())).Should().Be(family.Parent);
    }

    [Fact]
    public async Task ApproveMembershipOrder_WithAMemberWhoHasAnotherMembership_ThrowsAndChangesNothing()
    {
        var (orderId, family) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Family", 32000) with { MaxAdults = 2, IncludesChildren = true, RequiresApproval = true });
            var single = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Adult", 18000), sortOrder: 2);
            await MembershipSeed.SeedOrder(ctx, single, seeded.Partner, seeded.Partner);
            return ((await MembershipSeed.SeedOrder(ctx, plan, seeded.Parent, seeded.Parent)).Id, seeded);
        });

        var act = () => SendAsync(new ApproveMembershipOrder(TestDataSeed.ClubId, orderId, [family.Parent, family.Partner]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.AlreadyHasMembership));
        var order = await QueryAsync(ctx => ctx.MembershipOrders.Include(o => o.Members).SingleAsync(o => o.Id == orderId));
        order.Status.Should().Be(MembershipOrderStatus.AwaitingApproval);
        order.Members.Select(m => m.MemberId).Should().Equal(family.Parent);
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_WithMembers_EnrollsThePartnerTheMemberForgot()
    {
        // A couple plan needs no confirmation: the member ordered it alone and the treasurer adds the partner when the money arrives
        var (orderId, family) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Couple", 40000) with { MaxAdults = 2 });
            return ((await MembershipSeed.SeedOrder(ctx, plan, seeded.Parent, seeded.Parent)).Id, seeded);
        });

        var result = await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.BankTransfer, default, null, [family.Parent, family.Partner]));

        result.Status.Should().Be(OrderStatus.Paid);
        var enrolled = await QueryAsync(ctx => ctx.MemberSeasons.Where(ms => ms.MembershipOrderId == orderId).Select(ms => ms.MemberId).ToListAsync());
        enrolled.Should().BeEquivalentTo([family.Parent, family.Partner]);
    }

    [Fact]
    public async Task MarkMembershipOrderPaid_WithOtherMembersOnAPaidOrder_Throws()
    {
        var (orderId, family) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Couple", 40000) with { MaxAdults = 2 });
            return ((await MembershipSeed.SeedOrder(ctx, plan, seeded.Parent, seeded.Parent)).Id, seeded);
        });
        await SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.Cash, default, null));

        var act = () => SendAsync(new MarkMembershipOrderPaid(TestDataSeed.ClubId, TestDataSeed.AdminId, orderId, MembershipPaymentMethod.Cash, default, null, [family.Parent, family.Partner]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
        (await QueryAsync(ctx => ctx.MemberSeasons.CountAsync(ms => ms.MemberId == family.Partner))).Should().Be(0);
    }

    [Fact]
    public async Task ApproveMembershipOrder_WithSomebodyWhoIsNoMemberOfTheClub_Throws()
    {
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OrderAwaitingApproval(ctx));

        var act = () => SendAsync(new ApproveMembershipOrder(TestDataSeed.ClubId, orderId, [TestDataSeed.Member1Id, 987_654]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(RecordMembershipPayment.ErrorCode.MembershipMemberNotInClub));
    }

    [Fact]
    public async Task ApproveMembershipOrder_OrderThatIsNotWaitingForIt_Throws()
    {
        var orderId = await QueryAsync(ctx => MembershipPaymentSeed.OpenOrder(ctx));

        var act = () => SendAsync(new ApproveMembershipOrder(TestDataSeed.ClubId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
    }
}
