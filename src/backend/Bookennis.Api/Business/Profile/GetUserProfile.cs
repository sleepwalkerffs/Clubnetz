using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Profile;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

public record GetUserProfile(int UserId) : IQuery<GetUserProfileResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetUserProfile, GetUserProfileResult>
    {
        public async Task<GetUserProfileResult> Handle(GetUserProfile request, CancellationToken cancellationToken)
        {
            var userProfileResult = await (from user in context.Users

                                           let availableClubs = (from member in context.Set<Member>()
                                                                 where member.UserId == user.Id
                                                                 select member.ClubId).ToList()

                                           let hasProfilePicture = context.UserProfilePictures.Any(p => p.UserId == user.Id)

                                           where user.Id == request.UserId

                                           select new
                                           {
                                               user.FirstName,
                                               user.LastName,
                                               user.Birthday,
                                               Email = user.Email!,
                                               UserName = user.UserName!,
                                               Gender = (Gender)user.Gender,
                                               Language = (Language)user.Language,
                                               user.Street,
                                               user.City,
                                               user.ZipCode,
                                               Country = (Country)user.Country,
                                               AvailableClubs = availableClubs,
                                               FavoriteClubId = availableClubs.FirstOrDefault(), // TODO update as soon as we have a favorite club implemented
                                               HasProfilePicture = hasProfilePicture,
                                               BannerDismissed = user.ProfileCompletionBannerDismissedAt != null
                                           }
                                          ).SingleRequiredAsync(cancellationToken);

            // Not limited to the club of the request: the club switcher lists all of them
            var clubs = await (from member in context.Set<Member>().IgnoreQueryFilters()
                               join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id
                               where member.UserId == request.UserId
                               orderby club.Name
                               select new { club.Id, club.Name, member.UserRoles })
                              .AsNoTracking()
                              .ToListAsync(cancellationToken);

            return new GetUserProfileResult
            {
                FirstName = userProfileResult.FirstName,
                LastName = userProfileResult.LastName,
                Birthday = userProfileResult.Birthday,
                Email = userProfileResult.Email,
                UserName = userProfileResult.UserName,
                Gender = userProfileResult.Gender,
                Language = userProfileResult.Language,
                Street = userProfileResult.Street,
                City = userProfileResult.City,
                ZipCode = userProfileResult.ZipCode,
                Country = userProfileResult.Country,
                AvailableClubIds = userProfileResult.AvailableClubs,
                Clubs = clubs.ConvertAll(c => new ProfileClubDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Roles = Array.ConvertAll(c.UserRoles, role => (Bookennis.Shared.Controller.Shared.MemberRole)role)
                }),
                FavoriteClubId = userProfileResult.AvailableClubs.FirstOrDefault(),
                ProfilePictureUrl = userProfileResult.HasProfilePicture ? $"/api/Profile/picture/{request.UserId}" : null,
                ProfileCompletionBannerDismissed = userProfileResult.BannerDismissed
            };
        }
    }
}