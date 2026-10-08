using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetBadgeTierImage(int ClubId, int TierId) : IQuery<GetBadgeTierImageResult?>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBadgeTierImage, GetBadgeTierImageResult?>
    {
        public async Task<GetBadgeTierImageResult?> Handle(GetBadgeTierImage request, CancellationToken cancellationToken)
        {
            var image = await context.BadgeTierImages
                .Where(i => i.BadgeTierId == request.TierId)
                .Select(i => new GetBadgeTierImageResult(i.Data, i.ContentType))
                .FirstOrDefaultAsync(cancellationToken);

            return image;
        }
    }
}

public record GetBadgeTierImageResult(byte[] Data, string ContentType);
