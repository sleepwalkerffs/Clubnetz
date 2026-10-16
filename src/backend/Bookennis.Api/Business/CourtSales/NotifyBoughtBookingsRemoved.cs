using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Domain.CourtSales;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>
/// Bookings of bought slots were removed by the club (a court was blocked or set inactive, or an overbooking play mode took the slot).
/// <list type="bullet">
/// <item>A purchase that still waits for its payment is released (its payment page is closed, best effort; a payment that still
/// arrives books the slot again if it is free, otherwise the treasurers are told).</item>
/// <item>A paid purchase is <see cref="CourtPurchaseStatus.Compensated">compensated</see>: the buyer gets one booking credit per started
/// hour (the credit the purchase used comes back by itself and counts toward it) and an email. The treasurers are not mailed, the
/// purchase shows up in their list.</item>
/// <item>A purchase whose SEPA debit has not settled yet is left to the treasurers: the money is not there yet.</item>
/// </list>
/// </summary>
public record NotifyBoughtBookingsRemoved(IReadOnlyCollection<BoughtBookingRemoval> Removals) : ICommand
{
    public class Handler(AppDbContext context, ICourtPurchaseNotifier notifier, IMembershipCheckout checkout) : IRequestHandler<NotifyBoughtBookingsRemoved>
    {
        public async Task<Unit> Handle(NotifyBoughtBookingsRemoved request, CancellationToken cancellationToken)
        {
            var ids = request.Removals.Select(r => r.CourtPurchaseId).ToList();
            var purchases = await context.CourtPurchases.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;

            var held = purchases.Where(p => p.Status == CourtPurchaseStatus.AwaitingPayment).ToList();
            foreach (var purchase in held)
            {
                purchase.Expire();
                purchase.DetachBooking();
            }

            var paid = purchases.Where(p => p.Status == CourtPurchaseStatus.Paid).ToList();
            foreach (var purchase in paid)
            {
                purchase.Compensate(now);
                if (purchase is { GrantedCompensationCredits: > 0, MemberId: { } buyerId })
                {
                    context.Add(new BookingCreditGrant(
                        purchase.ClubId, buyerId, purchase.GrantedCompensationCredits, BookingCreditReason.BookingRemovedByClub, null, null));
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            foreach (var purchase in held.Where(p => p.StripeCheckoutSessionId is not null))
                await checkout.ExpireSession(purchase.ClubId, purchase.StripeCheckoutSessionId!, cancellationToken);

            foreach (var purchase in paid)
                await notifier.Compensated(purchase, request.Removals.First(r => r.CourtPurchaseId == purchase.Id), cancellationToken);

            foreach (var purchase in purchases.Where(p => p.Status == CourtPurchaseStatus.Processing))
                await notifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.BookingRemoved, cancellationToken);

            return default;
        }
    }
}
