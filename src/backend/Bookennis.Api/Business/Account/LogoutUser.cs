using Bookennis.Domain.User;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Account;

public record LogoutUser : ICommand
{
    public class Handler(SignInManager<User> signInManager) : IRequestHandler<LogoutUser>
    {
        public async Task<Unit> Handle(LogoutUser request, CancellationToken cancellationToken)
        {
            await signInManager.SignOutAsync();
            return default;
        }
    }
}
