using System.ComponentModel.DataAnnotations;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Account;

public record ConfirmEmail([Required, EmailAddress] string Email, [Required] string Token) : ICommand
{
    public class Handler(UserManager<User> userManager) : IRequestHandler<ConfirmEmail>
    {
        public enum ErrorCode
        {
            TokenInvalid
        }

        public async Task<Unit> Handle(ConfirmEmail request, CancellationToken cancellationToken)
        {
            // An unknown email is reported like an invalid token, so the link can't be used to probe for registered addresses
            var user = await userManager.FindByEmailAsync(request.Email) ?? throw new PreconditionException(ErrorCode.TokenInvalid, "Cannot confirm email. Token invalid");

            // The link was already used (e.g. clicked twice): nothing left to do
            if (user.EmailConfirmed)
                return default;

            var result = await userManager.ConfirmEmailAsync(user, request.Token);
            if (!result.Succeeded)
                throw new PreconditionException(ErrorCode.TokenInvalid, "Cannot confirm email. Token invalid");

            return default;
        }
    }
}
