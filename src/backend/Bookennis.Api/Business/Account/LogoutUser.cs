using Bookennis.Domain.User;
using Microsoft.AspNetCore.Identity;
using Bookennis.Api.Infrastructure.Audit;

namespace Bookennis.Api.Business.Account;

[SecurityAudit]
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
