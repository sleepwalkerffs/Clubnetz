using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Controller.Shared;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record GetAdminUser(int UserId) : IQuery<AdminUserDetailResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAdminUser, AdminUserDetailResult>
    {
        public async Task<AdminUserDetailResult> Handle(GetAdminUser request, CancellationToken cancellationToken)
        {
            var user = await context.Users
                .Where(u => u.Id == request.UserId)
                .Select(u => new AdminUserDetailResult
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    FullName = u.FullName,
                    Email = u.Email,
                    UserName = u.UserName,
                    EmailConfirmed = u.EmailConfirmed,
                    Birthday = u.Birthday,
                    Gender = (Gender)u.Gender,
                    Street = u.Street,
                    City = u.City,
                    ZipCode = u.ZipCode,
                    Country = (Country)u.Country
                })
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Domain.User.User), request.UserId);

            return user;
        }
    }
}
