using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Memberships;

/// <summary>
/// One membership fee of a season: a plan, who it covers, who pays and how far the payment is.
/// Name and amount are copied from the plan, so the order stays what was agreed when the plan changes later.
/// An order is one way to enroll members in a season; enrolling by hand works without one.
/// </summary>
[Guid("3F2D4A91-7B0E-4C6A-8D53-1A9E2B7C4D60")]
public class MembershipOrder : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNoteLength = 500;
    public const int MaxMembers = 20;
    public const int MaxStripeIdLength = 255;

    public enum ErrorCode
    {
        MembershipOrderMemberRequired = 0,
        MembershipOrderTooManyMembers = 1,
        MembershipOrderNoteTooLong = 2,

        /// <summary>The order is in a state that does not allow the action, e.g. paying an order that was cancelled.</summary>
        MembershipOrderInvalidStatus = 3,
    }

    /// <summary>The orders that hold a member's membership of a season: a member can only be in one of them per season.</summary>
    public static readonly MembershipOrderStatus[] ActiveStatuses =
    [
        MembershipOrderStatus.AwaitingApproval,
        MembershipOrderStatus.AwaitingPayment,
        MembershipOrderStatus.Processing,
        MembershipOrderStatus.Paid,
        MembershipOrderStatus.Disputed,
    ];

    private readonly List<MembershipOrderMember> members = new();

#pragma warning disable CS8618
    private MembershipOrder() { }
#pragma warning restore CS8618

    public MembershipOrder(MembershipPlan plan, int? payerMemberId, IReadOnlyCollection<int> memberIds, string? note = null)
    {
        var distinctMemberIds = DistinctMembers(memberIds);

        ClubId = plan.ClubId;
        SeasonId = plan.SeasonId;
        MembershipPlanId = plan.Id;
        PlanName = plan.Name;
        AmountCents = plan.PriceCents;
        Currency = plan.Currency;
        PayerMemberId = payerMemberId;
        Status = plan.RequiresApproval ? MembershipOrderStatus.AwaitingApproval : MembershipOrderStatus.AwaitingPayment;
        SetNote(note);

        members.AddRange(distinctMemberIds.Select(memberId => new MembershipOrderMember(this, memberId)));
    }

    public int SeasonId { get; private set; }
    public int MembershipPlanId { get; private set; }
    public string PlanName { get; private set; }
    public int AmountCents { get; private set; }
    public string Currency { get; private set; }

    /// <summary><c>null</c> once the payer left the club: the order stays as the club's record without a person.</summary>
    public int? PayerMemberId { get; private set; }

    public MembershipOrderStatus Status { get; private set; }
    public MembershipPaymentMethod? PaymentMethod { get; private set; }

    /// <summary>The Stripe Checkout Session the member can currently pay with.</summary>
    public string? StripeCheckoutSessionId { get; private set; }

    public string? StripePaymentIntentId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>The treasurer who recorded the payment by hand; <c>null</c> for online payments and in support mode.</summary>
    public int? MarkedPaidByMemberId { get; private set; }

    /// <summary>Only for the treasurers, never shown to the member.</summary>
    public string? Note { get; private set; }

    public IReadOnlyList<MembershipOrderMember> Members => members.AsReadOnly();

    public bool IsActive => ActiveStatuses.Contains(Status);
    public bool IsFree => AmountCents == 0;

    /// <summary>The member can still pay: nothing was paid and no debit is on its way.</summary>
    public bool IsPayable => Status is MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Failed;

    /// <summary>The member may withdraw the order as long as nothing was paid.</summary>
    public bool CanBeCancelledByMember => Status is MembershipOrderStatus.AwaitingApproval or MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Failed;

    /// <summary>A payment (online or recorded by hand) can be booked on the order.</summary>
    public bool CanBeMarkedPaid => Status is MembershipOrderStatus.AwaitingApproval or MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Processing or MembershipOrderStatus.Failed;

    public void Approve(DateTimeOffset approvedAt)
    {
        Require(Status == MembershipOrderStatus.AwaitingApproval);
        Status = MembershipOrderStatus.AwaitingPayment;
        ApprovedAt = approvedAt;
    }

    /// <summary>
    /// The treasurer corrects who the order covers, e.g. adds the partner the member forgot on a couple plan. The price is per order,
    /// so it stays the same. Possible until money is on its way or arrived: from then on the members are enrolled by the order.
    /// </summary>
    public void SetMembers(IReadOnlyCollection<int> memberIds)
    {
        Require(Status is MembershipOrderStatus.AwaitingApproval or MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Failed);
        var distinctMemberIds = DistinctMembers(memberIds);

        members.RemoveAll(m => !distinctMemberIds.Contains(m.MemberId));
        members.AddRange(distinctMemberIds.Where(id => members.All(m => m.MemberId != id)).Select(id => new MembershipOrderMember(this, id)).ToList());
    }

    public void Reject()
    {
        Require(Status == MembershipOrderStatus.AwaitingApproval);
        Status = MembershipOrderStatus.Cancelled;
    }

    /// <summary>Throws unless the order can be paid now. Checked before a payment page is created for it.</summary>
    public void EnsurePayable() => Require(IsPayable);

    /// <summary>A new Checkout Session was created. A failed order can be paid again.</summary>
    public void StartCheckout(string checkoutSessionId)
    {
        Require(IsPayable);
        Status = MembershipOrderStatus.AwaitingPayment;
        StripeCheckoutSessionId = checkoutSessionId;
    }

    /// <summary>The Checkout Session ran out without a payment. Sessions that were replaced by a newer one are ignored.</summary>
    public void CheckoutExpired(string checkoutSessionId)
    {
        if (StripeCheckoutSessionId == checkoutSessionId && Status == MembershipOrderStatus.AwaitingPayment)
            StripeCheckoutSessionId = null;
    }

    /// <summary>The member confirmed the payment, but the money is still on its way (SEPA direct debit). No-op when it already arrived.</summary>
    public void MarkProcessing(string? paymentIntentId)
    {
        if (Status is MembershipOrderStatus.Processing or MembershipOrderStatus.Paid)
        {
            StripePaymentIntentId ??= paymentIntentId;
            return;
        }

        Require(IsPayable);
        Status = MembershipOrderStatus.Processing;
        PaymentMethod = MembershipPaymentMethod.Online;
        StripePaymentIntentId = paymentIntentId;
    }

    /// <summary>The fee was paid. No-op on an order that is already paid, because Stripe may report a payment twice.</summary>
    public void MarkPaid(MembershipPaymentMethod method, DateTimeOffset paidAt, int? markedPaidByMemberId = null, string? paymentIntentId = null)
    {
        if (Status == MembershipOrderStatus.Paid)
        {
            StripePaymentIntentId ??= paymentIntentId;
            return;
        }

        Require(CanBeMarkedPaid);
        ApprovedAt ??= Status == MembershipOrderStatus.AwaitingApproval ? paidAt : null;
        Status = MembershipOrderStatus.Paid;
        PaymentMethod = method;
        PaidAt = paidAt;
        MarkedPaidByMemberId = method == MembershipPaymentMethod.Online ? null : markedPaidByMemberId;
        StripePaymentIntentId = paymentIntentId ?? StripePaymentIntentId;
    }

    /// <summary>A free plan has nothing to pay: the member confirms it instead. No-op when that already happened.</summary>
    public void ConfirmFree(DateTimeOffset confirmedAt)
    {
        if (Status == MembershipOrderStatus.Paid)
            return;

        Require(IsFree && CanBeMarkedPaid);
        Status = MembershipOrderStatus.Paid;
        PaidAt = confirmedAt;
    }

    /// <summary>The payment did not go through (e.g. a SEPA debit was returned). No-op when it already failed.</summary>
    public void MarkFailed()
    {
        if (Status == MembershipOrderStatus.Failed)
            return;

        Require(Status is MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Processing);
        Status = MembershipOrderStatus.Failed;
        StripeCheckoutSessionId = null;
    }

    /// <summary>The member withdraws an order that was not paid.</summary>
    public void CancelByMember()
    {
        Require(CanBeCancelledByMember);
        Status = MembershipOrderStatus.Cancelled;
    }

    /// <summary>
    /// The treasurer cancels the order, also a paid one (e.g. to record another plan). Money that was paid is given back outside of Clubnetz.
    /// An order with a debit on its way can't be cancelled, its result is still to come.
    /// </summary>
    public void CancelByTreasurer()
    {
        Require(Status is not (MembershipOrderStatus.Cancelled or MembershipOrderStatus.Processing or MembershipOrderStatus.Refunded));
        Status = MembershipOrderStatus.Cancelled;
    }

    /// <summary>The whole amount was refunded in Stripe. No-op when that is already known.</summary>
    public void MarkRefunded()
    {
        if (Status == MembershipOrderStatus.Refunded)
            return;

        Require(Status is MembershipOrderStatus.Paid or MembershipOrderStatus.Processing or MembershipOrderStatus.Disputed or MembershipOrderStatus.Cancelled);
        Status = MembershipOrderStatus.Refunded;
    }

    /// <summary>The payer disputes the payment at their bank. No-op when that is already known.</summary>
    public void MarkDisputed()
    {
        if (Status == MembershipOrderStatus.Disputed)
            return;

        Require(Status is MembershipOrderStatus.Paid or MembershipOrderStatus.Processing);
        Status = MembershipOrderStatus.Disputed;
    }

    public void SetNote(string? note)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > MaxNoteLength)
            throw new PreconditionException(ErrorCode.MembershipOrderNoteTooLong, [MaxNoteLength.ToString()], "The note is too long.");

        Note = trimmed;
    }

    /// <summary>The member left the club: the order stays, without the person.</summary>
    public void RemoveMember(int memberId)
    {
        members.RemoveAll(m => m.MemberId == memberId);
        if (PayerMemberId == memberId)
            PayerMemberId = null;

        if (MarkedPaidByMemberId == memberId)
            MarkedPaidByMemberId = null;
    }

    private static List<int> DistinctMembers(IReadOnlyCollection<int> memberIds)
    {
        var distinctMemberIds = memberIds.Distinct().ToList();
        if (distinctMemberIds.Count == 0)
            throw new PreconditionException(ErrorCode.MembershipOrderMemberRequired, "An order covers at least one member.");

        if (distinctMemberIds.Count > MaxMembers)
            throw new PreconditionException(ErrorCode.MembershipOrderTooManyMembers, [MaxMembers.ToString()], "An order covers too many members.");

        return distinctMemberIds;
    }

    private void Require(bool allowed)
    {
        if (!allowed)
            throw new PreconditionException(ErrorCode.MembershipOrderInvalidStatus, [Status.ToString()], $"The order is {Status}.");
    }
}
