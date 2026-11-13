using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

/// <summary>
/// The profile picture of a user. Only the user, application administrators and people who share a club with the user
/// (or play in ATP clubs that both are members of) see it, so a newly registered account can't collect the pictures of every member.
/// </summary>
public record GetProfilePicture(int UserId, int RequestingUserId, bool RequesterIsAdministrator) : IQuery<GetProfilePictureResult?>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetProfilePicture, GetProfilePictureResult?>
    {
        public async Task<GetProfilePictureResult?> Handle(GetProfilePicture request, CancellationToken cancellationToken)
        {
            if (!request.RequesterIsAdministrator && request.UserId != request.RequestingUserId && !await SharesAClub(request, cancellationToken))
                return null;

            var picture = await context.UserProfilePictures
                .Where(p => p.UserId == request.UserId)
                .Select(p => new { p.Data, p.ContentType })
                .FirstOrDefaultAsync(cancellationToken);

            return picture is null ? null : new GetProfilePictureResult(picture.Data, picture.ContentType);
        }

        /// <summary>Both users are members of the same club, or of two ATP clubs (members of ATP clubs play with each other).</summary>
        private Task<bool> SharesAClub(GetProfilePicture request, CancellationToken cancellationToken)
        {
            var members = context.Set<Member>().IgnoreQueryFilters();
            var clubs = context.Clubs.IgnoreQueryFilters();

            return (from mine in members
                    join myClub in clubs on mine.ClubId equals myClub.Id
                    from theirs in members
                    join theirClub in clubs on theirs.ClubId equals theirClub.Id
                    where mine.UserId == request.RequestingUserId
                       && theirs.UserId == request.UserId
                       && (mine.ClubId == theirs.ClubId || (myClub.IsAtpClub && theirClub.IsAtpClub))
                    select mine.Id).AnyAsync(cancellationToken);
        }
    }
}

public record GetProfilePictureResult(byte[] Data, string ContentType);
