using Bookennis.Api.Business.Users;
using Bookennis.Api.Data;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record DeleteAdminUser(int UserId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteAdminUser>
    {
        public async Task<Unit> Handle(DeleteAdminUser request, CancellationToken cancellationToken)
        {
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), request.UserId);

            await UserDeletion.DeleteUser(context, user, cancellationToken);
            return default;
        }
    }
}
