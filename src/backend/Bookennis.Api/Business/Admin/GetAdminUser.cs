using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Controller.Shared;
using Microsoft.EntityFrameworkCore;
using MemberRole = Bookennis.Shared.Controller.Shared.MemberRole;

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
                    Country = (Country)u.Country,
                    RegisteredAt = u.Metadata.Created
                })
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Domain.User.User), request.UserId);

            // A user can be club member and guest of the same club
            var clubs = await (from member in context.Set<Member>()
                               join club in context.Clubs on member.ClubId equals club.Id
                               where member.UserId == request.UserId
                               orderby club.Name, member.MemberType
                               select new AdminUserClubResult
                               {
                                   ClubId = club.Id,
                                   ClubName = club.Name,
                                   MemberId = member.Id,
                                   Roles = member.UserRoles.Select(r => (MemberRole)r).ToArray(),
                                   IsGuest = member.MemberType == MemberType.GuestMember
                               })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return user with { Clubs = clubs };
        }
    }
}
