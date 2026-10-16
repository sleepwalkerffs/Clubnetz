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
    /// The events of the connected account of these types whose delivery to a webhook endpoint of the platform failed, newest first,
    /// at most <paramref name="limit"/>. Stripe keeps events for 30 days.
    /// </summary>
    Task<IReadOnlyList<StripeEventInfo>> ListUndeliveredEvents(string accountId, IReadOnlyCollection<string> types, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the signature of a webhook request and reads what the event is about.
    /// Throws <see cref="StripeSignatureException"/> when the request was not sent by Stripe.
    /// </summary>
    StripeEventInfo ParseWebhookEvent(string payload, string? signatureHeader);
}

public record StripeAccountData(int ClubId, string ClubName, string? Email, string? WebsiteUrl, string Country);

public record StripeAccountInfo(string Id, bool ChargesEnabled, bool PayoutsEnabled, bool DetailsSubmitted);

/// <param name="ClubId">Stored on the session (metadata) next to the order id.</param>
/// <param name="OrderId">The membership order or the court purchase (<paramref name="Kind"/>); becomes the <c>client_reference_id</c> of the session.</param>
/// <param name="ProductName">What the member sees on the payment page, e.g. "Family - Season 2026".</param>
/// <param name="Description">Second line on the payment page, e.g. the names of the covered members.</param>
/// <param name="AmountCents">The price in the smallest unit of the currency.</param>
/// <param name="Currency">Three-letter ISO code in lower case.</param>
/// <param name="CustomerEmail">Prefills the email on the payment page.</param>
/// <param name="Locale">Language of the payment page (<c>de</c>, <c>en</c>).</param>
/// <param name="ApplicationFeeCents">The part of the amount that goes to the platform; <c>null</c> for none.</param>
/// <param name="SuccessUrl">Where the member is sent after paying.</param>
/// <param name="CancelUrl">Where the member is sent when they leave the payment page.</param>
/// <param name="Kind">What is paid: tells the sessions of membership orders and court purchases apart.</param>
/// <param name="ExpiresAt">When the payment page closes by itself (at least 30 minutes ahead); <c>null</c> for Stripe's default of 24 hours.</param>
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
    string CancelUrl,
    StripeCheckoutKind Kind = StripeCheckoutKind.MembershipOrder,
    DateTimeOffset? ExpiresAt = null);

/// <summary>What a Checkout Session pays for.</summary>
public enum StripeCheckoutKind
{
    /// <summary><c>client_reference_id</c> = the order id (as it always was), metadata <c>membership_order_id</c>.</summary>
    MembershipOrder,

    /// <summary><c>client_reference_id</c> = <c>court_purchase:{id}</c>, metadata <c>court_purchase_id</c>.</summary>
    CourtPurchase,
}

/// <summary>The reference a session carries in its <c>client_reference_id</c>, which tells what it pays for.</summary>
public static class StripeCheckoutReference
{
    public const string CourtPurchasePrefix = "court_purchase:";

    public static string For(StripeCheckoutKind kind, int id) => kind switch
    {
        StripeCheckoutKind.CourtPurchase => CourtPurchasePrefix + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => id.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Reads the kind and the id from a <c>client_reference_id</c>; <c>false</c> for anything the app did not create.</summary>
    public static bool TryParse(string? clientReferenceId, out StripeCheckoutKind kind, out int id)
    {
        kind = StripeCheckoutKind.MembershipOrder;
        id = 0;
        if (string.IsNullOrEmpty(clientReferenceId))
            return false;

        var value = clientReferenceId;
        if (value.StartsWith(CourtPurchasePrefix, StringComparison.Ordinal))
        {
            kind = StripeCheckoutKind.CourtPurchase;
            value = value[CourtPurchasePrefix.Length..];
        }

        return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id) && id > 0;
    }
}

/// <param name="Id">The id of the session.</param>
/// <param name="Url">The address of the payment page; <c>null</c> once the session is completed or expired.</param>
/// <param name="Status"><c>open</c>, <c>complete</c> or <c>expired</c>.</param>
/// <param name="PaymentStatus"><c>paid</c>, <c>unpaid</c> (also while a debit is on its way) or <c>no_payment_required</c>.</param>
/// <param name="ClientReferenceId">The order id the session was created for.</param>
/// <param name="ClubId">The club id from the metadata of the session.</param>
/// <param name="PaymentIntentId">The payment the session led to.</param>
/// <param name="Metadata">The metadata of the session (club id, order or purchase id).</param>
public record StripeCheckoutSession(string Id, string? Url, string? Status, string? PaymentStatus, string? ClientReferenceId, string? ClubId, string? PaymentIntentId, IReadOnlyDictionary<string, string>? Metadata = null)
{
    public const string PaymentStatusPaid = "paid";
    public const string PaymentStatusNoPaymentRequired = "no_payment_required";
    public const string StatusComplete = "complete";

    public bool IsPaid => PaymentStatus is PaymentStatusPaid or PaymentStatusNoPaymentRequired;
    public bool IsComplete => Status == StatusComplete;

    /// <summary>What the session pays for, read from the <c>client_reference_id</c>.</summary>
    public StripeCheckoutKind? Kind => StripeCheckoutReference.TryParse(ClientReferenceId, out var kind, out _) ? kind : null;

    /// <summary>The id of the order or purchase (<see cref="Kind"/>), read from the <c>client_reference_id</c>.</summary>
    public int? ReferenceId => StripeCheckoutReference.TryParse(ClientReferenceId, out _, out var id) ? id : null;

    /// <summary>The session was created for this court purchase.</summary>
    public bool IsForCourtPurchase(int purchaseId) => Kind == StripeCheckoutKind.CourtPurchase && ReferenceId == purchaseId;
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
