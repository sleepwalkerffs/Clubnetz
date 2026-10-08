using Bookennis.Api.Data;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record UpdateAdminUser(int UserId, string FirstName, string LastName, DateOnly Birthday, Gender Gender, string Street, string City, string ZipCode, Country Country) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateAdminUser>
    {
        public async Task<Unit> Handle(UpdateAdminUser request, CancellationToken cancellationToken)
        {
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), request.UserId);

            user.Update(request.FirstName, request.LastName, request.Birthday, request.Gender, user.Language, request.Street, request.City, request.ZipCode, request.Country);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
