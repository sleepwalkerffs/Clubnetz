using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Domain.CourtSales;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>
/// Bookings of bought slots were removed by the club (a court was blocked or set inactive, or an overbooking play mode took the slot).
/// A purchase that still waits for its payment is released (its payment page is closed, best effort; a payment that still arrives
/// books the slot again if it is free, otherwise the treasurers are told). Paid purchases stay paid: the treasurers are told so they
/// refund the buyer or arrange another time.
/// </summary>
public record NotifyBoughtBookingsRemoved(IReadOnlyCollection<int> CourtPurchaseIds) : ICommand
{
    public class Handler(AppDbContext context, ICourtPurchaseNotifier notifier, IMembershipCheckout checkout) : IRequestHandler<NotifyBoughtBookingsRemoved>
    {
        public async Task<Unit> Handle(NotifyBoughtBookingsRemoved request, CancellationToken cancellationToken)
        {
            var purchases = await context.CourtPurchases.Where(p => request.CourtPurchaseIds.Contains(p.Id)).ToListAsync(cancellationToken);

            var held = purchases.Where(p => p.Status == CourtPurchaseStatus.AwaitingPayment).ToList();
            foreach (var purchase in held)
            {
                purchase.Expire();
                purchase.DetachBooking();
            }

            await context.SaveChangesAsync(cancellationToken);

            foreach (var purchase in held.Where(p => p.StripeCheckoutSessionId is not null))
                await checkout.ExpireSession(purchase.ClubId, purchase.StripeCheckoutSessionId!, cancellationToken);

            foreach (var purchase in purchases.Except(held))
                await notifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.BookingRemoved, cancellationToken);

            return default;
        }
    }
}
