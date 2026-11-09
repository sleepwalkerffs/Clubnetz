using System.Globalization;
using System.Security.Claims;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Admin;

/// <summary>
/// Signs the application administrator in as another user. The session keeps who the administrator is (<see cref="ImpersonationClaims"/>):
/// the audit log names the administrator on everything done in it, the app shows a banner, and <c>StopImpersonation</c> goes back.
/// </summary>
[SecurityAudit]
public record ImpersonateUser(int UserId) : ICommand
{
    public enum ErrorCode
    {
        UserNotFound
    }

    public class Handler(UserManager<User> userManager, SignInManager<User> signInManager, IUserAccessor userAccessor) : IRequestHandler<ImpersonateUser>
    {
        public async Task<Unit> Handle(ImpersonateUser request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString(CultureInfo.InvariantCulture))
                ?? throw new PreconditionException(ErrorCode.UserNotFound, "User not found");

            var admin = userAccessor.User;
            // An administrator who impersonates from an impersonated session stays the one who started it
            var impersonatorId = admin.FindFirstValue(ImpersonationClaims.ImpersonatorId) ?? admin.GetId().ToString(CultureInfo.InvariantCulture);
            var impersonatorName = admin.FindFirstValue(ImpersonationClaims.ImpersonatorName) ?? admin.FindFirstValue(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Name) ?? "";

            await signInManager.SignInWithClaimsAsync(user, new AuthenticationProperties { IsPersistent = false },
                [new Claim(ImpersonationClaims.ImpersonatorId, impersonatorId), new Claim(ImpersonationClaims.ImpersonatorName, impersonatorName)]);
            return default;
        }
    }
}
