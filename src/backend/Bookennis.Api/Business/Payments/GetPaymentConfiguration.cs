using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>Whether the installation has Stripe keys and whether the club is connected. Without keys every online part is hidden.</summary>
public record GetPaymentConfiguration(int ClubId) : IQuery<PaymentConfigurationResult>
{
    public class Handler(AppDbContext context, IStripeGateway gateway) : IRequestHandler<GetPaymentConfiguration, PaymentConfigurationResult>
    {
        public async Task<PaymentConfigurationResult> Handle(GetPaymentConfiguration request, CancellationToken cancellationToken)
        {
            var account = await context.ClubPaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken);

            return new PaymentConfigurationResult
            {
                IsAvailable = gateway.IsConfigured,
                IsConnected = account is not null,
                CanAcceptPayments = gateway.IsConfigured && account is { ChargesEnabled: true },
            };
        }
    }
}
