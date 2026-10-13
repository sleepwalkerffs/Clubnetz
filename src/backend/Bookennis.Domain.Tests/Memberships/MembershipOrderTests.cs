using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Memberships;

public class MembershipOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_CopiesNameAndAmountOfThePlan()
    {
        var plan = Plan(21500);
        var order = new MembershipOrder(plan, payerMemberId: 1, [1, 2, 2, 3]);

        order.PlanName.Should().Be("Family");
        order.AmountCents.Should().Be(21500);
        order.Currency.Should().Be("eur");
        order.SeasonId.Should().Be(plan.SeasonId);
        order.ClubId.Should().Be(plan.ClubId);
        order.PayerMemberId.Should().Be(1);
        order.Members.Select(m => m.MemberId).Should().Equal(1, 2, 3);
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        order.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_PlanThatChangesLater_OrderKeepsWhatWasAgreed()
    {
        var plan = Plan(21500);
        var order = new MembershipOrder(plan, 1, [1]);

        plan.Update(Data(25000) with { Name = "Family 2027" });

        order.PlanName.Should().Be("Family");
        order.AmountCents.Should().Be(21500);
    }

    [Fact]
    public void Constructor_NoMember_Throws()
    {
        var act = () => new MembershipOrder(Plan(), 1, []);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderMemberRequired));
    }

    [Fact]
    public void Constructor_PlanNeedsConfirmation_StartsAwaitingApproval()
    {
        var order = new MembershipOrder(Plan(requiresApproval: true), 1, [1]);

        order.Status.Should().Be(MembershipOrderStatus.AwaitingApproval);
        order.IsPayable.Should().BeFalse();
        order.CanBeCancelledByMember.Should().BeTrue();
    }

    [Fact]
    public void Approve_MakesTheOrderPayable()
    {
        var order = new MembershipOrder(Plan(requiresApproval: true), 1, [1]);

        order.Approve(Now);

        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        order.ApprovedAt.Should().Be(Now);
        order.IsPayable.Should().BeTrue();
    }

    [Fact]
    public void SetMembers_BeforeTheConfirmation_ReplacesWhoIsCovered()
    {
        var order = new MembershipOrder(Plan(requiresApproval: true), 1, [1, 5]);

        order.SetMembers([1, 2, 2, 3]);

        order.Members.Select(m => m.MemberId).Should().BeEquivalentTo([1, 2, 3]);
        order.PayerMemberId.Should().Be(1);
        order.Status.Should().Be(MembershipOrderStatus.AwaitingApproval);
    }

    [Fact]
    public void SetMembers_NobodyOrAfterTheConfirmation_Throws()
    {
        var order = new MembershipOrder(Plan(requiresApproval: true), 1, [1]);

        var nobody = () => order.SetMembers([]);
        nobody.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderMemberRequired));

        // The member pays for what the confirmed order says
        order.Approve(Now);
        ShouldBeInvalid(() => order.SetMembers([1, 2]));
        order.Members.Select(m => m.MemberId).Should().Equal(1);
    }

    [Fact]
    public void Approve_OrderThatNeedsNoConfirmation_Throws()
    {
        var order = Order();

        ShouldBeInvalid(() => order.Approve(Now));
    }

    [Fact]
    public void Reject_CancelsTheOrder()
    {
        var order = new MembershipOrder(Plan(requiresApproval: true), 1, [1]);

        order.Reject();

        order.Status.Should().Be(MembershipOrderStatus.Cancelled);
        order.IsActive.Should().BeFalse();
        ShouldBeInvalid(order.Reject);
    }

    [Fact]
    public void StartCheckout_BeforeTheConfirmation_Throws()
    {
        var order = new MembershipOrder(Plan(requiresApproval: true), 1, [1]);

        ShouldBeInvalid(() => order.StartCheckout("cs_1"));
    }

    [Fact]
    public void StartCheckout_RemembersTheSessionAndANewOneReplacesIt()
    {
        var order = Order();

        order.StartCheckout("cs_1");
        order.StartCheckout("cs_2");

        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        order.StripeCheckoutSessionId.Should().Be("cs_2");
    }

    [Fact]
    public void CheckoutExpired_OnlyForTheCurrentSession()
    {
        var order = Order();
        order.StartCheckout("cs_1");
        order.StartCheckout("cs_2");

        order.CheckoutExpired("cs_1");
        order.StripeCheckoutSessionId.Should().Be("cs_2");

        order.CheckoutExpired("cs_2");
        order.StripeCheckoutSessionId.Should().BeNull();
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
    }

    [Fact]
    public void MarkPaid_Online_SetsStatusMethodAndDate()
    {
        var order = Order();
        order.StartCheckout("cs_1");

        order.MarkPaid(MembershipPaymentMethod.Online, Now, markedPaidByMemberId: 9, paymentIntentId: "pi_1");

        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.PaymentMethod.Should().Be(MembershipPaymentMethod.Online);
        order.PaidAt.Should().Be(Now);
        order.StripePaymentIntentId.Should().Be("pi_1");
        order.MarkedPaidByMemberId.Should().BeNull("nobody marks an online payment by hand");
    }

    [Fact]
    public void MarkPaid_Twice_IsANoOp()
    {
        var order = Order();
        order.MarkPaid(MembershipPaymentMethod.Online, Now, paymentIntentId: "pi_1");

        // Stripe may report a payment twice
        order.MarkPaid(MembershipPaymentMethod.Cash, Now.AddDays(3), markedPaidByMemberId: 9);

        order.PaymentMethod.Should().Be(MembershipPaymentMethod.Online);
        order.PaidAt.Should().Be(Now);
        order.MarkedPaidByMemberId.Should().BeNull();
    }

    [Fact]
    public void MarkPaid_ByTheTreasurer_RemembersWhoRecordedIt()
    {
        var order = Order();

        order.MarkPaid(MembershipPaymentMethod.BankTransfer, Now, markedPaidByMemberId: 9);

        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.PaymentMethod.Should().Be(MembershipPaymentMethod.BankTransfer);
        order.MarkedPaidByMemberId.Should().Be(9);
    }

    [Fact]
    public void MarkPaid_OrderAwaitingConfirmation_IsConfirmedWithThePayment()
    {
        var order = new MembershipOrder(Plan(requiresApproval: true), 1, [1]);

        order.MarkPaid(MembershipPaymentMethod.Cash, Now, 9);

        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.ApprovedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(MembershipOrderStatus.Cancelled)]
    [InlineData(MembershipOrderStatus.Refunded)]
    [InlineData(MembershipOrderStatus.Disputed)]
    public void MarkPaid_OrderThatIsOver_Throws(MembershipOrderStatus status)
    {
        var order = OrderIn(status);

        ShouldBeInvalid(() => order.MarkPaid(MembershipPaymentMethod.Online, Now));
    }

    [Fact]
    public void MarkProcessing_SepaDebitStarted_ThenPaid()
    {
        var order = Order();
        order.StartCheckout("cs_1");

        order.MarkProcessing("pi_1");

        order.Status.Should().Be(MembershipOrderStatus.Processing);
        order.PaymentMethod.Should().Be(MembershipPaymentMethod.Online);
        order.IsActive.Should().BeTrue();
        order.IsPayable.Should().BeFalse("the debit is on its way");
        order.CanBeCancelledByMember.Should().BeFalse();

        order.MarkPaid(MembershipPaymentMethod.Online, Now, paymentIntentId: "pi_1");
        order.Status.Should().Be(MembershipOrderStatus.Paid);
    }

    [Fact]
    public void MarkProcessing_AfterThePaymentArrived_IsANoOp()
    {
        var order = Order();
        order.MarkPaid(MembershipPaymentMethod.Online, Now);

        order.MarkProcessing("pi_1");

        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.StripePaymentIntentId.Should().Be("pi_1");
    }

    [Fact]
    public void MarkFailed_DebitReturned_OrderCanBePaidAgain()
    {
        var order = Order();
        order.StartCheckout("cs_1");
        order.MarkProcessing("pi_1");

        order.MarkFailed();

        order.Status.Should().Be(MembershipOrderStatus.Failed);
        order.IsActive.Should().BeFalse();
        order.IsPayable.Should().BeTrue();
        order.StripeCheckoutSessionId.Should().BeNull();

        order.StartCheckout("cs_2");
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
    }

    [Fact]
    public void MarkFailed_PaidOrder_Throws()
    {
        var order = OrderIn(MembershipOrderStatus.Paid);

        ShouldBeInvalid(order.MarkFailed);
    }

    [Fact]
    public void ConfirmFree_FreePlan_IsPaidWithoutAMethod()
    {
        var order = new MembershipOrder(Plan(0), 1, [1]);

        order.ConfirmFree(Now);

        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.PaymentMethod.Should().BeNull();
        order.PaidAt.Should().Be(Now);
    }

    [Fact]
    public void ConfirmFree_PlanWithAPrice_Throws()
    {
        var order = Order();

        ShouldBeInvalid(() => order.ConfirmFree(Now));
    }

    [Theory]
    [InlineData(MembershipOrderStatus.AwaitingApproval, true)]
    [InlineData(MembershipOrderStatus.AwaitingPayment, true)]
    [InlineData(MembershipOrderStatus.Failed, true)]
    [InlineData(MembershipOrderStatus.Processing, false)]
    [InlineData(MembershipOrderStatus.Paid, false)]
    [InlineData(MembershipOrderStatus.Disputed, false)]
    [InlineData(MembershipOrderStatus.Refunded, false)]
    [InlineData(MembershipOrderStatus.Cancelled, false)]
    public void CancelByMember_OnlyWhileNothingWasPaid(MembershipOrderStatus status, bool allowed)
    {
        var order = OrderIn(status);

        if (allowed)
        {
            order.CancelByMember();
            order.Status.Should().Be(MembershipOrderStatus.Cancelled);
        }
        else
        {
            ShouldBeInvalid(order.CancelByMember);
        }
    }

    [Theory]
    [InlineData(MembershipOrderStatus.AwaitingApproval, true)]
    [InlineData(MembershipOrderStatus.AwaitingPayment, true)]
    [InlineData(MembershipOrderStatus.Failed, true)]
    [InlineData(MembershipOrderStatus.Paid, true)]
    [InlineData(MembershipOrderStatus.Disputed, true)]
    [InlineData(MembershipOrderStatus.Processing, false)]
    [InlineData(MembershipOrderStatus.Refunded, false)]
    [InlineData(MembershipOrderStatus.Cancelled, false)]
    public void CancelByTreasurer_AlsoPaidOrdersButNoDebitOnItsWay(MembershipOrderStatus status, bool allowed)
    {
        var order = OrderIn(status);

        if (allowed)
        {
            order.CancelByTreasurer();
            order.Status.Should().Be(MembershipOrderStatus.Cancelled);
        }
        else
        {
            ShouldBeInvalid(order.CancelByTreasurer);
        }
    }

    [Fact]
    public void MarkRefunded_PaidOrder_AndTwiceIsANoOp()
    {
        var order = OrderIn(MembershipOrderStatus.Paid);

        order.MarkRefunded();
        order.MarkRefunded();

        order.Status.Should().Be(MembershipOrderStatus.Refunded);
        order.IsActive.Should().BeFalse();
    }

    [Fact]
    public void MarkRefunded_OrderThatWasNeverPaid_Throws()
    {
        ShouldBeInvalid(Order().MarkRefunded);
    }

    [Fact]
    public void MarkDisputed_PaidOrder_StaysTheMembershipOfItsMembers()
    {
        var order = OrderIn(MembershipOrderStatus.Paid);

        order.MarkDisputed();
        order.MarkDisputed();

        order.Status.Should().Be(MembershipOrderStatus.Disputed);
        order.IsActive.Should().BeTrue("the treasurer decides what happens to the membership");

        order.MarkRefunded();
        order.Status.Should().Be(MembershipOrderStatus.Refunded);
    }

    [Fact]
    public void MarkDisputed_OpenOrder_Throws()
    {
        ShouldBeInvalid(Order().MarkDisputed);
    }

    [Fact]
    public void RemoveMember_TakesThePersonOutAndKeepsTheOrder()
    {
        var order = new MembershipOrder(Plan(), 1, [1, 2]);
        order.MarkPaid(MembershipPaymentMethod.Cash, Now, markedPaidByMemberId: 1);

        order.RemoveMember(1);

        order.PayerMemberId.Should().BeNull();
        order.MarkedPaidByMemberId.Should().BeNull();
        order.Members.Select(m => m.MemberId).Should().Equal(2);
        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.AmountCents.Should().Be(21500);
    }

    [Fact]
    public void SetNote_TrimsAndRejectsTooLongNotes()
    {
        var order = Order();

        order.SetNote("  paid in the clubhouse  ");
        order.Note.Should().Be("paid in the clubhouse");

        order.SetNote("   ");
        order.Note.Should().BeNull();

        var act = () => order.SetNote(new string('x', MembershipOrder.MaxNoteLength + 1));
        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderNoteTooLong));
    }

    private static void ShouldBeInvalid(Action act)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));

    private static MembershipOrder Order() => new(Plan(), 1, [1]);

    private static MembershipOrder OrderIn(MembershipOrderStatus status)
    {
        var order = new MembershipOrder(Plan(requiresApproval: status == MembershipOrderStatus.AwaitingApproval), 1, [1]);
        switch (status)
        {
            case MembershipOrderStatus.AwaitingApproval:
            case MembershipOrderStatus.AwaitingPayment:
                break;
            case MembershipOrderStatus.Processing:
                order.MarkProcessing("pi_1");
                break;
            case MembershipOrderStatus.Paid:
                order.MarkPaid(MembershipPaymentMethod.Online, Now, paymentIntentId: "pi_1");
                break;
            case MembershipOrderStatus.Failed:
                order.MarkFailed();
                break;
            case MembershipOrderStatus.Cancelled:
                order.CancelByMember();
                break;
            case MembershipOrderStatus.Refunded:
                order.MarkPaid(MembershipPaymentMethod.Online, Now, paymentIntentId: "pi_1");
                order.MarkRefunded();
                break;
            case MembershipOrderStatus.Disputed:
                order.MarkPaid(MembershipPaymentMethod.Online, Now, paymentIntentId: "pi_1");
                order.MarkDisputed();
                break;
        }

        order.Status.Should().Be(status);
        return order;
    }

    private static MembershipPlan Plan(int priceCents = 21500, bool requiresApproval = false)
        => new(1, 5, Data(priceCents) with { RequiresApproval = requiresApproval }, 1);

    private static MembershipPlan.PlanData Data(int priceCents)
        => new("Family", null, priceCents, 2, true, null, null, false, false, false, true, null, null, true);
}
