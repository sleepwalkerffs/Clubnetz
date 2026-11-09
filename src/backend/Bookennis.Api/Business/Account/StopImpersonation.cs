using System.Security.Claims;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Account;

/// <summary>
/// Ends an impersonation (<c>ImpersonateUser</c>): signs the application administrator who started it back in. Only from a session
/// that was started that way, and only while that user is still an administrator; otherwise the session is just signed out.
/// </summary>
[SecurityAudit]
public record StopImpersonation : ICommand
{
    public enum ErrorCode
    {
        NotImpersonating
    }

    public class Handler(UserManager<User> userManager, SignInManager<User> signInManager, IUserAccessor userAccessor, IAuditContext auditContext) : IRequestHandler<StopImpersonation>
    {
        public async Task<Unit> Handle(StopImpersonation request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUser(out var principal) || !int.TryParse(principal.FindFirstValue(ImpersonationClaims.ImpersonatorId), out var adminId))
                throw new PreconditionException(ErrorCode.NotImpersonating, "The session is not impersonated");

            var admin = await userManager.FindByIdAsync(adminId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (admin is null || !await userManager.IsInRoleAsync(admin, nameof(UserRoles.Administrator)))
            {
                await signInManager.SignOutAsync();
                return default;
            }

            auditContext.SetSubject(admin.Id, admin.FullName);
            await signInManager.SignInAsync(admin, new AuthenticationProperties { IsPersistent = false });
            return default;
        }
    }
}
