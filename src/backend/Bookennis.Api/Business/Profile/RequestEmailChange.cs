using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Account;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Profile;

/// <summary>
/// Lets the current user change their own email address. The current password is required and the new address has to be confirmed via
/// the link that is sent to it. The email is only changed in <see cref="Admin.ConfirmEmailChange"/>, which also notifies the old address.
/// </summary>
public record RequestEmailChange([Required] string CurrentPassword, [Required, EmailAddress] string NewEmail) : ICommand
{
    public enum ErrorCode
    {
        PasswordMismatch,
        LockedOut,
        EmailUnchanged,
        DuplicateEmail
    }

    public class Handler(UserManager<User> userManager, SignInManager<User> signInManager, IUserAccessor userAccessor, IMediator mediator) : IRequestHandler<RequestEmailChange>
    {
        public async Task<Unit> Handle(RequestEmailChange request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var userId))
                throw new AuthorizationFailedException();

            var user = await userManager.FindByIdAsync(userId.ToString())
                ?? throw new AuthorizationFailedException();

            var passwordCheck = await signInManager.CheckPasswordSignInAsync(user, request.CurrentPassword, lockoutOnFailure: true);
            if (passwordCheck.IsLockedOut)
                throw new PreconditionException(ErrorCode.LockedOut, "The account is locked out.");
            if (!passwordCheck.Succeeded)
                throw new PreconditionException(ErrorCode.PasswordMismatch, "The password is incorrect.");

            var newEmail = request.NewEmail.Trim();
            if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                throw new PreconditionException(ErrorCode.EmailUnchanged, "The new email address is the same as the current one.");

            var existingUser = await userManager.FindByEmailAsync(newEmail);
            if (existingUser is not null && existingUser.Id != user.Id)
                throw new PreconditionException(ErrorCode.DuplicateEmail, "The email address is already in use by another user.");

            await mediator.Send(new SendEmailChangeLink(user.Id, newEmail, RequestedByAdmin: false), cancellationToken);

            return default;
        }
    }
}
