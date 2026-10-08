using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

public record GetProfilePicture(int UserId) : IQuery<GetProfilePictureResult?>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetProfilePicture, GetProfilePictureResult?>
    {
        public async Task<GetProfilePictureResult?> Handle(GetProfilePicture request, CancellationToken cancellationToken)
        {
            var picture = await context.UserProfilePictures
                .Where(p => p.UserId == request.UserId)
                .Select(p => new { p.Data, p.ContentType })
                .FirstOrDefaultAsync(cancellationToken);

            return picture is null ? null : new GetProfilePictureResult(picture.Data, picture.ContentType);
        }
    }
}

public record GetProfilePictureResult(byte[] Data, string ContentType);
