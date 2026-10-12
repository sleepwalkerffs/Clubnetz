using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

public static class PaymentAccountMapper
{
    public static PaymentAccountDto ToDto(this ClubPaymentAccount? account, bool isAvailable) => new()
    {
        IsAvailable = isAvailable,
        IsConnected = account is not null,
        ChargesEnabled = account?.ChargesEnabled ?? false,
        PayoutsEnabled = account?.PayoutsEnabled ?? false,
        DetailsSubmitted = account?.DetailsSubmitted ?? false,
        ConnectedAt = account?.ConnectedAt,
    };
}
