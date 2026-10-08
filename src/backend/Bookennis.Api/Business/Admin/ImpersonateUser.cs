using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Admin;

public record ImpersonateUser(int UserId) : ICommand
{
    public enum ErrorCode
    {
        UserNotFound
    }

    public class Handler(UserManager<User> userManager, SignInManager<User> signInManager) : IRequestHandler<ImpersonateUser>
    {
        public async Task<Unit> Handle(ImpersonateUser request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString())
                ?? throw new PreconditionException(ErrorCode.UserNotFound, "User not found");

            await signInManager.SignInAsync(user, new AuthenticationProperties { IsPersistent = false });
            return default;
        }
    }
}
