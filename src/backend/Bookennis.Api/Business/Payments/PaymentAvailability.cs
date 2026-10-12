using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

public enum PaymentErrorCode
{
    /// <summary>The installation has no Stripe keys: online payment is switched off.</summary>
    PaymentsNotConfigured = 0,

    /// <summary>The club has no Stripe account, or the account can't take payments yet.</summary>
    PaymentAccountNotReady = 1,

    /// <summary>Stripe did not answer or declined the request.</summary>
    PaymentProviderUnavailable = 2,
}

/// <summary>Online payment was asked for, but the installation has no Stripe keys.</summary>
public class PaymentsNotConfiguredException() : PreconditionException(PaymentErrorCode.PaymentsNotConfigured, "Online payment is not configured.");

public interface IPaymentAvailability
{
    /// <summary>The installation has Stripe keys.</summary>
    bool IsConfigured { get; }

    /// <summary>Members of the club can pay online: the installation has keys and the club's Stripe account takes payments.</summary>
    Task<bool> CanAcceptPayments(int clubId, CancellationToken cancellationToken);
}

public class PaymentAvailability(AppDbContext context, IStripeGateway gateway) : IPaymentAvailability
{
    public bool IsConfigured => gateway.IsConfigured;

    public async Task<bool> CanAcceptPayments(int clubId, CancellationToken cancellationToken)
        => gateway.IsConfigured
        && await context.ClubPaymentAccounts.AnyAsync(a => a.ClubId == clubId && a.ChargesEnabled, cancellationToken);
}
