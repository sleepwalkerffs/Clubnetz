using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>The Stripe account of the club as the payment settings show it.</summary>
public record GetPaymentAccount(int ClubId) : IQuery<PaymentAccountDto>
{
    public class Handler(AppDbContext context, IStripeGateway gateway) : IRequestHandler<GetPaymentAccount, PaymentAccountDto>
    {
        public async Task<PaymentAccountDto> Handle(GetPaymentAccount request, CancellationToken cancellationToken)
        {
            var account = await context.ClubPaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken);
            return account.ToDto(gateway.IsConfigured);
        }
    }
}
