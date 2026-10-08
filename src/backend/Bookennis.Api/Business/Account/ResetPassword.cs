using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Account;

public record ResetPassword(string Email, string Token, string NewPassword) : ICommand
{
    public class Handler(UserManager<User> userManager) : IRequestHandler<ResetPassword>
    {
        public enum Error
        {
            IdentityError,
        }

        public async Task<Unit> Handle(ResetPassword request, CancellationToken cancellationToken)
        {
            // An unknown email is reported like an invalid token, so the reset can't be used to probe for registered addresses
            var user = await userManager.FindByEmailAsync(request.Email)
                    ?? throw new PreconditionException(Error.IdentityError, [nameof(IdentityErrorDescriber.InvalidToken)], "Error when changing the password.");

            var resetResult = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!resetResult.Succeeded)
                //maybe replace this with the precondition exception from neovac
                throw new PreconditionException(Error.IdentityError, resetResult.Errors.Select(x => x.Code).ToArray(), "Error when changing the password.");

            // A successful reset proves access to the mailbox, so it also lifts a lockout caused by failed logins
            await userManager.ResetAccessFailedCountAsync(user);
            await userManager.SetLockoutEndDateAsync(user, null);

            //we also count that as email confirmation, as the user can only come to that place when the user clicked a link in the email the user got
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }

            return default;
        }
    }
}
