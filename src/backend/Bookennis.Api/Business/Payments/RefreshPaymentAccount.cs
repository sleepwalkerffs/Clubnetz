using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>Asks Stripe for the status of the club's account ("check again"). An account that no longer exists at Stripe is disconnected.</summary>
public record RefreshPaymentAccount(int ClubId) : ICommand<PaymentAccountDto>
{
    public class Handler(AppDbContext context, IStripeGateway gateway) : IRequestHandler<RefreshPaymentAccount, PaymentAccountDto>
    {
        public async Task<PaymentAccountDto> Handle(RefreshPaymentAccount request, CancellationToken cancellationToken)
        {
            if (!gateway.IsConfigured)
                throw new PaymentsNotConfiguredException();

            var account = await context.ClubPaymentAccounts.FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken);
            if (account is null)
                return account.ToDto(isAvailable: true);

            var info = await StripeCalls.Run(() => gateway.GetAccount(account.StripeAccountId, cancellationToken));
            if (info is null)
            {
                context.ClubPaymentAccounts.Remove(account);
                account = null;
            }
            else
            {
                account.UpdateStatus(info.ChargesEnabled, info.PayoutsEnabled, info.DetailsSubmitted);
            }

            await context.SaveChangesAsync(cancellationToken);
            return account.ToDto(isAvailable: true);
        }
    }
}
