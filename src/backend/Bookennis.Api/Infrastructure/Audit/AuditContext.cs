using System.Globalization;
using System.Security.Claims;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Api.Infrastructure.Tenant;
using Fusonic.Extensions.Common.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>Who did something, taken from the request (or the background job) the audit entry is written in.</summary>
public sealed record AuditActor(
    int? UserId,
    string? Name,
    string? Email,
    int? ImpersonatorUserId,
    string? ImpersonatorName,
    int? ApiKeyId,
    string? System,
    string? IpAddress,
    string? UserAgent,
    string? Request)
{
    public void ApplyTo(AuditEvent e)
    {
        e.ActorUserId = UserId;
        e.ActorName = Name;
        e.ActorEmail = Email;
        e.ImpersonatorUserId = ImpersonatorUserId;
        e.ImpersonatorName = ImpersonatorName;
        e.ApiKeyId = ApiKeyId;
        e.System = System;
        e.IpAddress = IpAddress;
        e.UserAgent = UserAgent;
        e.Request = Request;
    }
}

/// <summary>
/// The audit state of one scope (an HTTP request or a background job): its correlation id, who acts, and what the running command
/// says about the user it is about (<see cref="SetSubject"/>).
/// </summary>
public interface IAuditContext
{
    public Guid CorrelationId { get; }

    public AuditActor GetActor();

    public int? GetClubId();

    /// <summary>Tells the audit log which user the running command is about (the account that signs in, is impersonated, gets a new password, ...).</summary>
    public void SetSubject(int userId, string? name);

    public (int? UserId, string? Name) Subject { get; }

    /// <summary>How many data changes were recorded in this scope so far (to leave out background commands that changed nothing).</summary>
    public int DataChangeCount { get; }

    public void AddDataChanges(int count);

    /// <summary>Called by the command decorator when a new top-level command starts.</summary>
    public void ResetSubject();

    /// <summary>The user is deleted by the running command: once it succeeded, name and email are taken out of the audit log (<see cref="IAuditWriter.PseudonymizeUserAsync"/>).</summary>
    public void PseudonymizeAfterCommand(int userId);

    public IReadOnlyList<int> TakeUsersToPseudonymize();
}

public sealed class AuditContext(IUserAccessor userAccessor, IHttpContextAccessor httpContextAccessor, ITenantService tenantService) : IAuditContext
{
    private const int MaxUserAgentLength = 256;
    private const int MaxRequestLength = 512;

    public Guid CorrelationId { get; } = Guid.NewGuid();

    public (int? UserId, string? Name) Subject { get; private set; }

    public int DataChangeCount { get; private set; }

    public void SetSubject(int userId, string? name) => Subject = (userId, name);

    public void ResetSubject()
    {
        Subject = default;
        usersToPseudonymize.Clear();
    }

    private readonly List<int> usersToPseudonymize = [];

    public void PseudonymizeAfterCommand(int userId) => usersToPseudonymize.Add(userId);

    public IReadOnlyList<int> TakeUsersToPseudonymize()
    {
        var users = usersToPseudonymize.ToList();
        usersToPseudonymize.Clear();
        return users;
    }

    public void AddDataChanges(int count) => DataChangeCount += count;

    public int? GetClubId() => tenantService.GetTenantId();

    public AuditActor GetActor()
    {
        var http = httpContextAccessor.HttpContext;
        var principal = userAccessor.TryGetUser(out var user) ? user : http?.User;

        int? userId = null;
        string? name = null, email = null, impersonatorName = null;
        int? impersonatorId = null, apiKeyId = null;

        if (principal?.Identity?.IsAuthenticated == true)
        {
            if (int.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), CultureInfo.InvariantCulture, out var id))
                userId = id;
            name = principal.FindFirstValue(JwtRegisteredClaimNames.Name);
            email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            if (int.TryParse(principal.FindFirstValue(ImpersonationClaims.ImpersonatorId), CultureInfo.InvariantCulture, out var impersonator))
            {
                impersonatorId = impersonator;
                impersonatorName = principal.FindFirstValue(ImpersonationClaims.ImpersonatorName);
            }

            if (int.TryParse(principal.GetClubApiKeyId(), CultureInfo.InvariantCulture, out var keyId))
                apiKeyId = keyId;
        }

        string? system = null, ip = null, userAgent = null, request = null;
        if (http is null)
        {
            system = AuditJobContext.Current ?? "System";
        }
        else
        {
            ip = http.Connection.RemoteIpAddress?.ToString();
            userAgent = Truncate(http.Request.Headers.UserAgent.ToString(), MaxUserAgentLength);
            request = Truncate($"{http.Request.Method} {http.Request.Path}", MaxRequestLength);
        }

        return new AuditActor(userId, name, email, impersonatorId, impersonatorName, apiKeyId, system, ip, userAgent, request);
    }

    private static string? Truncate(string? value, int length) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= length ? value : value[..length];
}

/// <summary>The name of the background job that runs on this async flow, set by <see cref="AuditJobFilter"/>.</summary>
public static class AuditJobContext
{
    private static readonly AsyncLocal<string?> current = new();

    public static string? Current
    {
        get => current.Value;
        set => current.Value = value;
    }
}

/// <summary>Claims a session gets when an application administrator signs in as somebody else (<c>ImpersonateUser</c>).</summary>
public static class ImpersonationClaims
{
    public const string ImpersonatorId = "impersonator_id";
    public const string ImpersonatorName = "impersonator_name";

    public static bool IsImpersonated(this ClaimsPrincipal principal) => principal.HasClaim(c => c.Type == ImpersonatorId);
}
