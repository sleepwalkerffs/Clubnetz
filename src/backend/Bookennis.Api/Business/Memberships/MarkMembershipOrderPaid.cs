using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Memberships;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// The treasurer records that the fee of an existing order arrived outside of Clubnetz (bank transfer, cash). The members are enrolled.
/// In support mode there is no member: <c>MarkedPaidByMemberId</c> stays empty then.
/// </summary>
public record MarkMembershipOrderPaid(int ClubId, int UserId, int MembershipOrderId, MembershipPaymentMethod Method, DateOnly PaidOn, string? Note) : ICommand<MembershipOrderDto>
{
    public class Handler(AppDbContext context, IMembershipFulfillment fulfillment, IMembershipNotifier notifier, IMembershipCheckout checkout)
        : IRequestHandler<MarkMembershipOrderPaid, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(MarkMembershipOrderPaid request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);
            var treasurerMemberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var openCheckoutSessionId = order.StripeCheckoutSessionId;
            var wasSettled = order.Status.IsSettledOrSettling();

            if (order.IsFree)
                order.ConfirmFree(MembershipPayments.PaidAt(request.PaidOn));
            else
                order.MarkPaid(MembershipPayments.ManualMethod(request.Method), MembershipPayments.PaidAt(request.PaidOn), treasurerMemberId);

            if (request.Note is not null)
                order.SetNote(request.Note);

            if (!wasSettled)
                await fulfillment.Complete(order, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            if (openCheckoutSessionId is not null)
                await checkout.ExpireSession(order.ClubId, openCheckoutSessionId, cancellationToken);

            if (!wasSettled && !order.IsFree)
                await notifier.PaymentReceived(order, cancellationToken);

            return await order.ToDto(context, cancellationToken);
        }
    }
}

public static class MembershipPayments
{
    /// <summary>A payment the treasurer records is never an online payment: those only come from Stripe.</summary>
    public static MembershipPaymentMethod ManualMethod(MembershipPaymentMethod method)
        => method == MembershipPaymentMethod.Online ? MembershipPaymentMethod.Other : method;

    /// <summary>The day the money arrived, at noon so it stays that day in the time zone of the club. No day or a day in the future is now.</summary>
    public static DateTimeOffset PaidAt(DateOnly paidOn)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (paidOn == default || paidOn >= today)
            return DateTimeOffset.UtcNow;

        return new DateTimeOffset(paidOn.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    }
}
