using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Profile;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.EntityFrameworkCore;

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
                FavoriteClubId = userProfileResult.AvailableClubs.FirstOrDefault(),
                ProfilePictureUrl = userProfileResult.HasProfilePicture ? $"/api/Profile/picture/{request.UserId}" : null,
                ProfileCompletionBannerDismissed = userProfileResult.BannerDismissed
            };
        }
    }
}