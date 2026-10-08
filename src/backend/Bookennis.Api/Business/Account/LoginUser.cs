using System.ComponentModel.DataAnnotations;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Account;

/// <summary>
/// Signs the user in. Failed attempts count towards the account lockout (see <c>IdentityAndAuthenticationConfiguration</c>).
/// An unknown email and a wrong password both answer with <see cref="ErrorCode.InvalidCredentials"/>, so the login can't be used to find
/// out which email addresses are registered.
/// </summary>
public record LoginUser([Required, EmailAddress] string Email, [Required] string Password, [Required] bool RememberMe) : ICommand
{
    public enum ErrorCode
    {
        InvalidCredentials,
        LockedOut,
        NotAllowed
    }

    public class Handler(UserManager<User> userManager, SignInManager<User> signInManager) : IRequestHandler<LoginUser>
    {
        public async Task<Unit> Handle(LoginUser request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByEmailAsync(request.Email) ?? throw InvalidCredentials();
            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await signInManager.SignInAsync(user, new AuthenticationProperties { IsPersistent = request.RememberMe });
                return default;
            }

            if (result.IsLockedOut)
                throw new PreconditionException(ErrorCode.LockedOut, "User is locked out");

            // Identity checks the email confirmation before the password. Only tell the user to confirm the email if the password was
            // right, otherwise this would reveal that the address is registered.
            if (result.IsNotAllowed && await userManager.CheckPasswordAsync(user, request.Password))
                throw new PreconditionException(ErrorCode.NotAllowed, "User is not allowed");

            throw InvalidCredentials();
        }

        private static PreconditionException InvalidCredentials() => new(ErrorCode.InvalidCredentials, "Invalid email or password");
    }
}
