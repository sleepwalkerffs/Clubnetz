using System.Security.Cryptography;
using Bookennis.Api.Business.Clubhouse.SumUp;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Clubhouse;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ClubhouseTabPaymentStatus = Bookennis.Domain.Clubhouse.ClubhouseTabPaymentStatus;

namespace Bookennis.Api.Business.Clubhouse;

/// <summary>
/// The <c>state</c> of the OAuth flow: which club the host connects and who started it, encrypted and valid for a short time. The
/// callback only accepts it from the browser of the same user, so nobody can slip their own SumUp account into a host's clubhouse.
/// </summary>
public class SumUpOAuthState(IDataProtectionProvider provider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly ITimeLimitedDataProtector protector = provider.CreateProtector("Bookennis.Clubhouse.SumUp.OAuthState.v1").ToTimeLimitedDataProtector();

    public string Create(int clubId, int userId)
        => protector.Protect($"{clubId}:{userId}:{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}", Lifetime);

    public bool TryRead(string? state, out int clubId, out int userId)
    {
        clubId = 0;
        userId = 0;
        if (string.IsNullOrWhiteSpace(state))
            return false;

        string text;
        try
        {
            text = protector.Unprotect(state, out _);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var parts = text.Split(':');
        return parts.Length == 3 && int.TryParse(parts[0], out clubId) && int.TryParse(parts[1], out userId);
    }
}

public static class ClubhouseSumUpRules
{
    /// <summary>
    /// Where the host's money goes is decided by the host or an admin of the club who is a member of it - not in support mode, where
    /// there is no member. The callback of SumUp has no club in its route, so the handlers check it themselves.
    /// </summary>
    public static async Task<int> GetRequiredHostMemberId(this AppDbContext context, int clubId, int userId, CancellationToken cancellationToken)
    {
        var memberId = await context.ClubMembers.IgnoreQueryFilters()
            .Where(m => m.ClubId == clubId && m.UserId == userId
                        && (m.UserRoles.Contains(MemberRole.ClubhouseHost) || m.UserRoles.Contains(MemberRole.Admin)))
            .OrderBy(m => m.Id)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return memberId ?? throw new AccessDeniedException("Only the clubhouse host or an admin of the club connects the SumUp account.");
    }
}

/// <summary>The SumUp account of the clubhouse host: status only, never a token.</summary>
public record GetClubhouseSumUpAccount(int ClubId) : IQuery<ClubhouseSumUpAccountDto>
{
    public class Handler(AppDbContext context, ISumUpGateway gateway) : IRequestHandler<GetClubhouseSumUpAccount, ClubhouseSumUpAccountDto>
    {
        public async Task<ClubhouseSumUpAccountDto> Handle(GetClubhouseSumUpAccount request, CancellationToken cancellationToken)
        {
            var account = await context.ClubhouseSumUpAccounts.AsNoTracking()
                .Where(a => a.ClubId == request.ClubId)
                .Select(a => new { a.MerchantCode, a.MerchantName, a.ConnectedAt, a.NeedsReconnect })
                .FirstOrDefaultAsync(cancellationToken);

            var pending = await context.ClubhouseTabPayments
                .CountAsync(p => p.ClubId == request.ClubId && p.Status == ClubhouseTabPaymentStatus.Pending, cancellationToken);

            return new ClubhouseSumUpAccountDto
            {
                IsConfigured = gateway.IsConfigured,
                IsConnected = account is not null,
                NeedsReconnect = account?.NeedsReconnect ?? false,
                MerchantCode = account?.MerchantCode,
                MerchantName = account?.MerchantName,
                ConnectedAt = account?.ConnectedAt,
                PendingPayments = pending,
            };
        }
    }
}

/// <summary>The page of SumUp where the host signs in and allows Clubnetz to take payments to the host's account.</summary>
public record StartClubhouseSumUpConnect(int ClubId, int UserId) : IQuery<ClubhouseRedirectResult>
{
    public class Handler(AppDbContext context, ISumUpGateway gateway, SumUpOAuthState state, AppSettings appSettings)
        : IRequestHandler<StartClubhouseSumUpConnect, ClubhouseRedirectResult>
    {
        public async Task<ClubhouseRedirectResult> Handle(StartClubhouseSumUpConnect request, CancellationToken cancellationToken)
        {
            if (!gateway.IsConfigured)
                throw new PreconditionException(ClubhouseErrorCode.ClubhouseOnlinePaymentNotAvailable, "SumUp is not configured.");

            await context.GetRequiredHostMemberId(request.ClubId, request.UserId, cancellationToken);
            return new ClubhouseRedirectResult { Url = gateway.AuthorizeUrl(state.Create(request.ClubId, request.UserId), appSettings.SumUpRedirectUri()) };
        }
    }
}

/// <summary>
/// The host comes back from SumUp (the OAuth callback): exchanges the code for the tokens, reads the merchant account and stores both.
/// Returns the page of the app to send the browser to; it never fails with an error page, because the browser arrives from SumUp.
/// </summary>
public record CompleteClubhouseSumUpConnect(int UserId, string? Code, string? State, string? Error) : ICommand<string>
{
    public class Handler(
        AppDbContext context,
        ISumUpGateway gateway,
        SumUpOAuthState oauthState,
        SumUpTokenProtector protector,
        AppSettings appSettings,
        ILogger<CompleteClubhouseSumUpConnect> logger) : IRequestHandler<CompleteClubhouseSumUpConnect, string>
    {
        public async Task<string> Handle(CompleteClubhouseSumUpConnect request, CancellationToken cancellationToken)
        {
            if (!oauthState.TryRead(request.State, out var clubId, out var userId))
                return "/";

            // The state was started by somebody else (a link sent to the host): nothing is connected
            if (userId != request.UserId)
                return "/accessdenied";

            int memberId;
            try
            {
                memberId = await context.GetRequiredHostMemberId(clubId, request.UserId, cancellationToken);
            }
            catch (AccessDeniedException)
            {
                return "/accessdenied";
            }

            var page = $"/clubs/{clubId}/clubhouse?view=sumup";
            if (!string.IsNullOrEmpty(request.Error) || string.IsNullOrEmpty(request.Code) || !gateway.IsConfigured)
                return $"{page}&sumup=failed";

            SumUpTokens tokens;
            SumUpMerchant merchant;
            try
            {
                tokens = await gateway.ExchangeCode(request.Code, appSettings.SumUpRedirectUri(), cancellationToken);
                if (tokens.RefreshToken is null)
                    throw new SumUpException("SumUp returned no refresh token.");

                merchant = await gateway.GetMerchant(tokens.AccessToken, cancellationToken);
            }
            catch (SumUpException e)
            {
                logger.LogWarning(e, "Connecting the SumUp account of club {ClubId} failed.", clubId);
                return $"{page}&sumup=failed";
            }

            var encrypted = protector.Protect(tokens);
            var now = DateTimeOffset.UtcNow;
            var account = await context.ClubhouseSumUpAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.ClubId == clubId, cancellationToken);
            if (account is null)
                context.ClubhouseSumUpAccounts.Add(new ClubhouseSumUpAccount(clubId, merchant.MerchantCode, merchant.Name, memberId, encrypted, now));
            else
                account.Reconnect(merchant.MerchantCode, merchant.Name, memberId, encrypted, now);

            await context.SaveChangesAsync(cancellationToken);
            return $"{page}&sumup=connected";
        }
    }
}

/// <summary>
/// Disconnects the host's SumUp account: members can't pay in the app any more. Not while a member is paying, because that payment
/// could not be checked any more. The access stays allowed at SumUp until the host removes it there.
/// </summary>
public record DisconnectClubhouseSumUp(int ClubId, int UserId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DisconnectClubhouseSumUp>
    {
        public async Task<Unit> Handle(DisconnectClubhouseSumUp request, CancellationToken cancellationToken)
        {
            await context.GetRequiredHostMemberId(request.ClubId, request.UserId, cancellationToken);

            var account = await context.ClubhouseSumUpAccounts.FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken);
            if (account is null)
                return default;

            if (await context.ClubhouseTabPayments.AnyAsync(p => p.ClubId == request.ClubId && p.Status == ClubhouseTabPaymentStatus.Pending, cancellationToken))
                throw new PreconditionException(ClubhouseErrorCode.ClubhouseSumUpPaymentsRunning, "A member is paying a tab right now.");

            context.ClubhouseSumUpAccounts.Remove(account);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
