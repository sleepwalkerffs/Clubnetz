using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Memberships;

/// <summary>Whether membership fees can be paid online in this club.</summary>
public record PaymentConfigurationResult
{
    /// <summary>The installation has Stripe keys. Without them the online part is switched off everywhere.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>The club has connected a Stripe account.</summary>
    public required bool IsConnected { get; init; }

    /// <summary>The connected account finished its onboarding and can take payments.</summary>
    public required bool CanAcceptPayments { get; init; }
}

/// <summary>The Stripe account of the club.</summary>
public record PaymentAccountDto
{
    public required bool IsAvailable { get; init; }
    public required bool IsConnected { get; init; }
    public required bool ChargesEnabled { get; init; }
    public required bool PayoutsEnabled { get; init; }

    /// <summary>The club went through Stripe's onboarding form. Stripe may still be checking the details.</summary>
    public required bool DetailsSubmitted { get; init; }

    public required DateTimeOffset? ConnectedAt { get; init; }
}

/// <summary>Starts or continues connecting the club with Stripe.</summary>
public record ConnectPaymentAccountModel
{
    /// <summary>The country the club is based in. It is only used when the account is created and can not be changed afterwards.</summary>
    public Country Country { get; init; } = Country.Austria;
}

/// <summary>The address of the onboarding page of Stripe for the account of the club.</summary>
public record ConnectPaymentAccountResult(string Url);
