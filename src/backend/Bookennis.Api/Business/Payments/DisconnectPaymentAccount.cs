using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>
/// Disconnects the club from Stripe: members can no longer pay online. The Stripe account itself stays with the club,
/// and orders that were paid stay as they are. Works without Stripe keys, so a club can always be disconnected.
/// </summary>
public record DisconnectPaymentAccount(int ClubId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DisconnectPaymentAccount>
    {
        public async Task<Unit> Handle(DisconnectPaymentAccount request, CancellationToken cancellationToken)
        {
            var accounts = await context.ClubPaymentAccounts.Where(a => a.ClubId == request.ClubId).ToListAsync(cancellationToken);
            context.ClubPaymentAccounts.RemoveRange(accounts);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
