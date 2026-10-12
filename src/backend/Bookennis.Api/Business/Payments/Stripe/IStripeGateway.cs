namespace Bookennis.Api.Business.Payments.Stripe;

/// <summary>
/// Everything the app asks Stripe. No other class talks to Stripe, so tests replace this interface and the rest stays testable.
/// The connected account of a club is always passed explicitly: a call for a club runs on the account of that club.
/// </summary>
public interface IStripeGateway
{
    /// <summary>The installation has Stripe keys. Every other method throws <see cref="PaymentsNotConfiguredException"/> without them.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Creates the connected account of a club: the club is the merchant, sees the full Stripe dashboard,
    /// pays the Stripe fees and carries its disputes. Returns the account id.
    /// </summary>
    Task<string> CreateAccount(StripeAccountData data, CancellationToken cancellationToken);

    /// <summary>A link to the onboarding form of Stripe for the account. It is valid for a few minutes and used once.</summary>
    Task<string> CreateAccountLink(string accountId, string refreshUrl, string returnUrl, CancellationToken cancellationToken);

    /// <summary><c>null</c> when the account does not exist (any more) or the platform has no access to it.</summary>
    Task<StripeAccountInfo?> GetAccount(string accountId, CancellationToken cancellationToken);

    /// <summary>Creates a Checkout Session on the connected account (direct charge) and returns the address of the payment page.</summary>
    Task<StripeCheckoutSession> CreateCheckoutSession(string accountId, StripeCheckoutData data, CancellationToken cancellationToken);

    Task<StripeCheckoutSession?> GetCheckoutSession(string accountId, string sessionId, CancellationToken cancellationToken);

    /// <summary>Closes the payment page of a session. Does nothing when the session is already completed or expired.</summary>
    Task ExpireCheckoutSession(string accountId, string sessionId, CancellationToken cancellationToken);

    Task<StripeChargeInfo?> GetCharge(string accountId, string chargeId, CancellationToken cancellationToken);
    Task<StripeDisputeInfo?> GetDispute(string accountId, string disputeId, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the signature of a webhook request and reads what the event is about.
    /// Throws <see cref="StripeSignatureException"/> when the request was not sent by Stripe.
    /// </summary>
    StripeEventInfo ParseWebhookEvent(string payload, string? signatureHeader);
}

public record StripeAccountData(int ClubId, string ClubName, string? Email, string? WebsiteUrl, string Country);

public record StripeAccountInfo(string Id, bool ChargesEnabled, bool PayoutsEnabled, bool DetailsSubmitted);

/// <param name="ClubId">Stored on the session (metadata) next to the order id.</param>
/// <param name="OrderId">Becomes the <c>client_reference_id</c> of the session.</param>
/// <param name="ProductName">What the member sees on the payment page, e.g. "Family - Season 2026".</param>
/// <param name="Description">Second line on the payment page, e.g. the names of the covered members.</param>
/// <param name="AmountCents">The price in the smallest unit of the currency.</param>
/// <param name="Currency">Three-letter ISO code in lower case.</param>
/// <param name="CustomerEmail">Prefills the email on the payment page.</param>
/// <param name="Locale">Language of the payment page (<c>de</c>, <c>en</c>).</param>
/// <param name="ApplicationFeeCents">The part of the amount that goes to the platform; <c>null</c> for none.</param>
/// <param name="SuccessUrl">Where the member is sent after paying.</param>
/// <param name="CancelUrl">Where the member is sent when they leave the payment page.</param>
public record StripeCheckoutData(
    int ClubId,
    int OrderId,
    string ProductName,
    string? Description,
    int AmountCents,
    string Currency,
    string? CustomerEmail,
    string Locale,
    long? ApplicationFeeCents,
    string SuccessUrl,
    string CancelUrl);

/// <param name="Id">The id of the session.</param>
/// <param name="Url">The address of the payment page; <c>null</c> once the session is completed or expired.</param>
/// <param name="Status"><c>open</c>, <c>complete</c> or <c>expired</c>.</param>
/// <param name="PaymentStatus"><c>paid</c>, <c>unpaid</c> (also while a debit is on its way) or <c>no_payment_required</c>.</param>
/// <param name="ClientReferenceId">The order id the session was created for.</param>
/// <param name="ClubId">The club id from the metadata of the session.</param>
/// <param name="PaymentIntentId">The payment the session led to.</param>
public record StripeCheckoutSession(string Id, string? Url, string? Status, string? PaymentStatus, string? ClientReferenceId, string? ClubId, string? PaymentIntentId)
{
    public const string PaymentStatusPaid = "paid";
    public const string PaymentStatusNoPaymentRequired = "no_payment_required";

    public bool IsPaid => PaymentStatus is PaymentStatusPaid or PaymentStatusNoPaymentRequired;
}

/// <param name="Id">The id of the charge.</param>
/// <param name="PaymentIntentId">The payment the charge belongs to.</param>
/// <param name="IsFullyRefunded">The whole amount was given back.</param>
public record StripeChargeInfo(string Id, string? PaymentIntentId, bool IsFullyRefunded);

public record StripeDisputeInfo(string Id, string? PaymentIntentId);

/// <param name="Id">The id of the event, the same for every delivery of it.</param>
/// <param name="Type">E.g. <c>checkout.session.completed</c>.</param>
/// <param name="AccountId">The connected account the event belongs to; <c>null</c> for events of the platform account.</param>
/// <param name="ObjectId">The id of the object the event is about.</param>
public record StripeEventInfo(string Id, string Type, string? AccountId, string ObjectId);

/// <summary>A webhook request whose signature is missing or wrong.</summary>
public class StripeSignatureException(string message, Exception? innerException = null) : Exception(message, innerException);
