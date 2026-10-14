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

public class RecordMembershipPaymentTests : TestBase
{
    private readonly IMembershipNotifier notifier;

    public RecordMembershipPaymentTests(TestFixture fixture) : base(fixture)
    {
        notifier = GetInstance<IMembershipNotifier>();
        notifier.ClearReceivedCalls();
    }

    [Fact]
    public async Task RecordMembershipPayment_CreatesAPaidOrderAndEnrolls()
    {
        var (family, planId) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            return (seeded, (await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.FamilyData())).Id);
        });

        var result = await SendAsync(new RecordMembershipPayment(
            TestDataSeed.ClubId, TestDataSeed.AdminId, planId, family.Everybody, family.Partner, MembershipPaymentMethod.Cash, MembershipSeed.Today, "Paid at the opening party"));

        result.Should().BeEquivalentTo(new { Status = OrderStatus.Paid, AmountCents = 21500, PayerMemberId = family.Partner }, o => o.ExcludingMissingMembers());

        var order = await QueryAsync(ctx => ctx.MembershipOrders.Include(o => o.Members).SingleAsync());
        order.Should().BeEquivalentTo(new { PaymentMethod = MembershipPaymentMethod.Cash, MarkedPaidByMemberId = TestDataSeed.Member2Id, Note = "Paid at the opening party" }, o => o.ExcludingMissingMembers());

        var enrollments = await QueryAsync(ctx => ctx.MemberSeasons.ToListAsync());
        enrollments.Select(e => e.MemberId).Should().BeEquivalentTo(family.Everybody);
        enrollments.Should().OnlyContain(e => e.MembershipOrderId == order.Id && e.SeasonId == family.SeasonId);

        await notifier.Received(1).PaymentReceived(Arg.Is<MembershipOrder>(o => o.Id == order.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordMembershipPayment_TreasurerDecides_RulesOfThePlanAreNotEnforced()
    {
        // A plan members can't choose themselves, for a member who is too young for it and not in a family
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(
            ctx,
            (await MembershipSeed.SeedSeason(ctx)).Id,
            MembershipSeed.Data("Senior, honorary", 5000) with { BirthYearTo = 1956, IsSelfService = false, RequiresApproval = true, NewMembersOnly = true })).Id);

        var result = await SendAsync(new RecordMembershipPayment(TestDataSeed.ClubId, TestDataSeed.AdminId, planId, [TestDataSeed.Member1Id], null, MembershipPaymentMethod.BankTransfer, default, null));

        result.Status.Should().Be(OrderStatus.Paid);
        result.PayerMemberId.Should().Be(TestDataSeed.Member1Id, "without a payer the first member pays");
        result.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordMembershipPayment_FreePlan_EnrollsWithoutAnEmail()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Honorary", 0) with { IsSelfService = false })).Id);

        var result = await SendAsync(new RecordMembershipPayment(TestDataSeed.ClubId, TestDataSeed.AdminId, planId, [TestDataSeed.Member1Id], null, MembershipPaymentMethod.Cash, default, null));

        result.Status.Should().Be(OrderStatus.Paid);
        result.PaymentMethod.Should().BeNull();
        (await QueryAsync(ctx => ctx.MemberSeasons.CountAsync())).Should().Be(1);
        await notifier.DidNotReceiveWithAnyArgs().PaymentReceived(default!, default);
    }

    [Fact]
    public async Task RecordMembershipPayment_MemberWhoAlreadyHasAMembership_Throws()
    {
        var planId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            return plan.Id;
        });

        var act = () => SendAsync(new RecordMembershipPayment(TestDataSeed.ClubId, TestDataSeed.AdminId, planId, [TestDataSeed.Member1Id], null, MembershipPaymentMethod.Cash, default, null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.AlreadyHasMembership));
        (await QueryAsync(ctx => ctx.MembershipOrders.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task RecordMembershipPayment_MemberOfAnotherClub_Throws()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);

        var act = () => SendAsync(new RecordMembershipPayment(TestDataSeed.ClubId, TestDataSeed.AdminId, planId, [987654], null, MembershipPaymentMethod.Cash, default, null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(RecordMembershipPayment.ErrorCode.MembershipMemberNotInClub));
    }

    [Fact]
    public async Task RecordMembershipPayment_NoMember_Throws()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);

        var act = () => SendAsync(new RecordMembershipPayment(TestDataSeed.ClubId, TestDataSeed.AdminId, planId, [], null, MembershipPaymentMethod.Cash, default, null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderMemberRequired));
    }
}
