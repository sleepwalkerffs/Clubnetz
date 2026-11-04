using Bookennis.Domain.Base;

namespace Bookennis.Domain.Clubhouse;

/// <summary>
/// The SumUp account of the clubhouse host (one per club), connected with OAuth: members pay their tabs in the app and the money goes
/// straight to this account, not to the club and not to Clubnetz. The tokens are stored encrypted (the domain only sees the encrypted
/// text) and never leave the server. Deleting the row disconnects the account.
/// </summary>
public class ClubhouseSumUpAccount : TenantDomainEntity
{
    public const int MaxMerchantCodeLength = 32;
    public const int MaxMerchantNameLength = 200;

#pragma warning disable CS8618
    private ClubhouseSumUpAccount() { }
#pragma warning restore CS8618

    public ClubhouseSumUpAccount(int clubId, string merchantCode, string? merchantName, int? connectedByMemberId, EncryptedTokens tokens, DateTimeOffset now)
    {
        ClubId = clubId;
        MerchantCode = merchantCode;
        AccessToken = tokens.AccessToken;
        RefreshToken = tokens.RefreshToken ?? throw new ArgumentException("A new connection always has a refresh token.", nameof(tokens));
        Reconnect(merchantCode, merchantName, connectedByMemberId, tokens, now);
    }

    public string MerchantCode { get; private set; }
    public string? MerchantName { get; private set; }

    /// <summary>The access token, encrypted. Valid until <see cref="AccessTokenExpiresAt"/> (about an hour).</summary>
    public string AccessToken { get; private set; }

    public DateTimeOffset AccessTokenExpiresAt { get; private set; }

    /// <summary>The refresh token, encrypted. SumUp may replace it with every refresh: always keep the newest one.</summary>
    public string RefreshToken { get; private set; }

    public DateTimeOffset ConnectedAt { get; private set; }

    /// <summary><c>null</c> in support mode and once the member left the club.</summary>
    public int? ConnectedByMemberId { get; private set; }

    /// <summary>
    /// SumUp refused the refresh token (revoked by the host, or expired): no payment can be started or checked until the host
    /// connects the account again.
    /// </summary>
    public bool NeedsReconnect { get; private set; }

    /// <summary>The access token runs out within <paramref name="margin"/>: refresh it before a call.</summary>
    public bool AccessTokenExpires(DateTimeOffset now, TimeSpan margin) => AccessTokenExpiresAt - margin <= now;

    /// <summary>The host connected the account (again), possibly another one.</summary>
    public void Reconnect(string merchantCode, string? merchantName, int? connectedByMemberId, EncryptedTokens tokens, DateTimeOffset now)
    {
        MerchantCode = merchantCode;
        MerchantName = string.IsNullOrWhiteSpace(merchantName) ? null : merchantName.Trim()[..Math.Min(merchantName.Trim().Length, MaxMerchantNameLength)];
        ConnectedByMemberId = connectedByMemberId;
        ConnectedAt = now;
        UpdateTokens(tokens);
    }

    public void UpdateTokens(EncryptedTokens tokens)
    {
        AccessToken = tokens.AccessToken;
        AccessTokenExpiresAt = tokens.AccessTokenExpiresAt;

        // SumUp sends a new refresh token only sometimes; without one the old one stays valid
        if (tokens.RefreshToken is not null)
            RefreshToken = tokens.RefreshToken;

        NeedsReconnect = false;
    }

    public void MarkNeedsReconnect() => NeedsReconnect = true;

    /// <summary>The encrypted tokens; <c>RefreshToken</c> is <c>null</c> when SumUp did not send a new one.</summary>
    public record EncryptedTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string? RefreshToken);
}
