using System.Security.Cryptography;
using System.Transactions;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubhouse;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubhouse.SumUp;

/// <summary>
/// Encrypts the tokens of the hosts' SumUp accounts for the database (ASP.NET Core data protection, the key ring of the installation).
/// When the key ring is lost the tokens can't be read any more and the host has to connect again.
/// </summary>
public class SumUpTokenProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Bookennis.Clubhouse.SumUp.Tokens.v1");

    public ClubhouseSumUpAccount.EncryptedTokens Protect(SumUpTokens tokens)
        => new(protector.Protect(tokens.AccessToken), tokens.ExpiresAt, tokens.RefreshToken is null ? null : protector.Protect(tokens.RefreshToken));

    /// <summary>Throws <see cref="SumUpAuthorizationException"/> when the text can't be decrypted (key ring lost).</summary>
    public string Unprotect(string encrypted)
    {
        try
        {
            return protector.Unprotect(encrypted);
        }
        catch (CryptographicException)
        {
            throw new SumUpAuthorizationException("The stored SumUp token can't be decrypted.");
        }
    }
}

/// <summary>The access token of a host's SumUp account, refreshed when it is about to run out.</summary>
public interface ISumUpAccessTokens
{
    /// <summary>
    /// The plain access token for a call to SumUp. Throws <see cref="SumUpAuthorizationException"/> when the account has to be
    /// connected again (it is marked so), and <see cref="SumUpException"/> when SumUp could not be reached.
    /// </summary>
    Task<string> Get(ClubhouseSumUpAccount account, CancellationToken cancellationToken);
}

public class SumUpAccessTokens(AppDbContext context, SumUpTokenProtector protector, ISumUpTokenRefresher refresher) : ISumUpAccessTokens
{
    /// <summary>A token that runs out this soon is refreshed first, so it does not expire during the call.</summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    public async Task<string> Get(ClubhouseSumUpAccount account, CancellationToken cancellationToken)
    {
        if (!account.NeedsReconnect && account.AccessTokenExpires(DateTimeOffset.UtcNow, RefreshMargin))
        {
            // The new tokens are saved in a transaction of their own: they stay even when the rest of the request fails
            await refresher.Refresh(account.Id, cancellationToken);
            await context.Entry(account).ReloadAsync(cancellationToken);
        }

        if (account.NeedsReconnect)
            throw new SumUpAuthorizationException("The SumUp account has to be connected again.");

        try
        {
            return protector.Unprotect(account.AccessToken);
        }
        catch (SumUpAuthorizationException)
        {
            // The key ring that encrypted the token is gone: only connecting again helps, and nobody should try until then
            await refresher.MarkNeedsReconnect(account.Id, cancellationToken);
            throw;
        }
    }
}

/// <summary>Refreshes the tokens of an account and saves them at once, whatever happens to the request that needed them.</summary>
public interface ISumUpTokenRefresher
{
    /// <summary>
    /// Refreshes the tokens of the account unless somebody else just did. When SumUp refuses the refresh token the account is marked
    /// <see cref="ClubhouseSumUpAccount.NeedsReconnect"/>. Throws <see cref="SumUpException"/> when SumUp could not be reached.
    /// </summary>
    Task Refresh(int accountId, CancellationToken cancellationToken);

    /// <summary>Marks the account <see cref="ClubhouseSumUpAccount.NeedsReconnect"/>, saved at once like a refresh.</summary>
    Task MarkNeedsReconnect(int accountId, CancellationToken cancellationToken);
}

/// <summary>
/// SumUp may replace the refresh token with every refresh, so a refreshed token must never be lost in a rolled back transaction and two
/// requests must never refresh at the same time (the second would use a refresh token that is no longer valid). It runs in its own
/// transaction and connection under an advisory lock per account, and reads the account again once it has the lock.
/// </summary>
public class SumUpTokenRefresher(AppSettings appSettings, ISumUpGateway gateway, SumUpTokenProtector protector, ILogger<SumUpTokenRefresher> logger)
    : ISumUpTokenRefresher
{
    private const long LockNamespace = 0x53554D5550L << 32; // "SUMUP"

    public async Task Refresh(int accountId, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled);
        await using var context = new AppDbContext(appSettings.ConnectionString);

        await Lock(context, accountId, cancellationToken);
        await RefreshLocked(context, gateway, protector, logger, accountId, cancellationToken);

        scope.Complete();
    }

    public async Task MarkNeedsReconnect(int accountId, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled);
        await using var context = new AppDbContext(appSettings.ConnectionString);

        await Lock(context, accountId, cancellationToken);
        await MarkNeedsReconnectLocked(context, accountId, cancellationToken);

        scope.Complete();
    }

    public static async Task MarkNeedsReconnectLocked(AppDbContext context, int accountId, CancellationToken cancellationToken)
    {
        var account = await context.ClubhouseSumUpAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null || account.NeedsReconnect)
            return;

        account.MarkNeedsReconnect();
        await context.SaveChangesAsync(cancellationToken);
    }

    public static Task Lock(AppDbContext context, int accountId, CancellationToken cancellationToken)
    {
        var key = LockNamespace + accountId;
        return context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    /// <summary>The refresh itself, once the lock is taken. The changes are saved on <paramref name="context"/>.</summary>
    public static async Task RefreshLocked(AppDbContext context, ISumUpGateway gateway, SumUpTokenProtector protector, ILogger logger, int accountId, CancellationToken cancellationToken)
    {
        // A background job has no tenant: the account is found by its id
        var account = await context.ClubhouseSumUpAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        // Disconnected, already marked, or refreshed by another request while this one waited for the lock
        if (account is null || account.NeedsReconnect || !account.AccessTokenExpires(DateTimeOffset.UtcNow, SumUpAccessTokens.RefreshMargin))
            return;

        try
        {
            var tokens = await gateway.RefreshTokens(protector.Unprotect(account.RefreshToken), cancellationToken);
            account.UpdateTokens(protector.Protect(tokens));
        }
        catch (SumUpAuthorizationException e)
        {
            logger.LogWarning(e, "The SumUp account of club {ClubId} has to be connected again.", account.ClubId);
            account.MarkNeedsReconnect();
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
