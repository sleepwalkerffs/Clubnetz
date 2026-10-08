using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Shared;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Profile;

public record ChangePassword([Required] string CurrentPassword, [Required] string NewPassword) : ICommand
{
    public class Handler(UserManager<User> userManager, SignInManager<User> signInManager, IUserAccessor userAccessor) : IRequestHandler<ChangePassword>
    {
        public async Task<Unit> Handle(ChangePassword request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var userId))
                throw new AuthorizationFailedException();

            var user = await userManager.FindByIdAsync(userId.ToString())
                ?? throw new AuthorizationFailedException();

            var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                throw new PreconditionException(AccountErrorCode.IdentityError, result.Errors.Select(x => x.Code).ToArray(), "Error when changing the password.");

            // The security stamp changed, so the current cookie has to be renewed to keep the user signed in
            await signInManager.RefreshSignInAsync(user);

            return default;
        }
    }
}
