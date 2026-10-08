using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetOneTimeBadgeImage(int ClubId, int OneTimeBadgeId) : IQuery<GetOneTimeBadgeImageResult?>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetOneTimeBadgeImage, GetOneTimeBadgeImageResult?>
    {
        public async Task<GetOneTimeBadgeImageResult?> Handle(GetOneTimeBadgeImage request, CancellationToken cancellationToken)
        {
            var image = await context.OneTimeBadgeImages
                .Where(i => i.OneTimeBadgeId == request.OneTimeBadgeId)
                .Select(i => new GetOneTimeBadgeImageResult(i.Data, i.ContentType))
                .FirstOrDefaultAsync(cancellationToken);

            return image;
        }
    }
}

public record GetOneTimeBadgeImageResult(byte[] Data, string ContentType);
