using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Account;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Admin;

public record ChangeUserEmail([Required] int UserId, [Required, EmailAddress] string NewEmail) : ICommand
{
    public enum ErrorCode
    {
        EmailAlreadyInUse,
        InvalidEmail
    }

    public class Handler(UserManager<User> userManager, IMediator mediator) : IRequestHandler<ChangeUserEmail>
    {
        public async Task<Unit> Handle(ChangeUserEmail request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString())
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), request.UserId);

            var newEmail = request.NewEmail.Trim();

            // Validate that the new email is not already used
            var existingUser = await userManager.FindByEmailAsync(newEmail);
            if (existingUser is not null && existingUser.Id != user.Id)
                throw new PreconditionException(ErrorCode.EmailAlreadyInUse, "The email address is already in use by another user.");

            await mediator.Send(new SendEmailChangeLink(user.Id, newEmail, RequestedByAdmin: true), cancellationToken);

            return default;
        }
    }
}
