namespace Bookennis.Api.Business.Clubhouse.SumUp;

/// <summary>
/// Everything the app asks SumUp. No other class talks to SumUp, so tests replace this interface. The calls for a host's account
/// take its access token (plain text, only ever in memory): get it from <see cref="ISumUpAccessTokens"/>.
/// </summary>
public interface ISumUpGateway
{
    /// <summary>The installation has an OAuth app at SumUp. Every other method throws without it.</summary>
    bool IsConfigured { get; }

    /// <summary>The page of SumUp where the host signs in and allows Clubnetz to take payments for them.</summary>
    string AuthorizeUrl(string state, string redirectUri);

    /// <summary>Exchanges the code of the OAuth callback for the tokens of the host.</summary>
    Task<SumUpTokens> ExchangeCode(string code, string redirectUri, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="SumUpAuthorizationException"/> when SumUp refuses the refresh token: the host has to connect again.</summary>
    Task<SumUpTokens> RefreshTokens(string refreshToken, CancellationToken cancellationToken);

    /// <summary>The merchant account the token belongs to.</summary>
    Task<SumUpMerchant> GetMerchant(string accessToken, CancellationToken cancellationToken);

    /// <summary>Creates a checkout with a hosted payment page on the account of the token.</summary>
    Task<SumUpCheckout> CreateCheckout(string accessToken, SumUpCheckoutData data, CancellationToken cancellationToken);

    /// <summary><c>null</c> when the checkout does not exist on the account of the token.</summary>
    Task<SumUpCheckout?> GetCheckout(string accessToken, string checkoutId, CancellationToken cancellationToken);

    /// <summary>Closes a checkout that was not paid, so its page can't be used any more. Does nothing for a paid or closed one.</summary>
    Task DeactivateCheckout(string accessToken, string checkoutId, CancellationToken cancellationToken);
}

/// <summary>The tokens of a host. <c>RefreshToken</c> is <c>null</c> when SumUp did not send a new one (the old one stays valid).</summary>
public record SumUpTokens(string AccessToken, DateTimeOffset ExpiresAt, string? RefreshToken, string? Scope);

public record SumUpMerchant(string MerchantCode, string? Name);

/// <summary>
/// A checkout to create. <c>Reference</c> is our own reference (<c>checkout_reference</c>, unique per merchant), <c>AmountCents</c> is in
/// the smallest unit (SumUp gets major units), <c>Description</c> is shown on the payment page and in the host's SumUp dashboard,
/// <c>ReturnUrl</c> is the webhook SumUp posts to when the status changes, <c>RedirectUrl</c> where the member is sent back to.
/// </summary>
public record SumUpCheckoutData(
    string MerchantCode,
    string Reference,
    int AmountCents,
    string Currency,
    string Description,
    DateTimeOffset ValidUntil,
    string ReturnUrl,
    string RedirectUrl);

/// <summary>
/// The checkout as SumUp has it now. <c>Status</c>: <c>PENDING</c>, <c>FAILED</c> (the last attempt failed, the member may try again),
/// <c>PAID</c> or <c>EXPIRED</c>. <c>TransactionCode</c>: the code of the successful transaction, if there is one.
/// </summary>
public record SumUpCheckout(
    string Id,
    string? Reference,
    string? MerchantCode,
    int AmountCents,
    string? Currency,
    string Status,
    string? HostedCheckoutUrl,
    string? TransactionCode)
{
    public bool IsPaid => Status == "PAID";
    public bool IsExpired => Status == "EXPIRED";
}

/// <summary>SumUp declined a request or could not be reached.</summary>
public class SumUpException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>SumUp refused the token (revoked, expired): the host has to connect the account again.</summary>
public class SumUpAuthorizationException(string message) : SumUpException(message);
