using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Bookennis.Api.Business.Badges;

public record UploadBadgeTierImage(int ClubId, int TierId, IFormFile File) : ICommand
{
    public enum ErrorCode
    {
        UnsupportedImageType = 0,
        FileTooLarge = 1,
        BadgeTierNotFound = 2,
        CouldNotDecodeImage = 3
    }

    private static readonly HashSet<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int MaxDimension = 128;

    public class Handler(AppDbContext context) : IRequestHandler<UploadBadgeTierImage>
    {
        public async Task<Unit> Handle(UploadBadgeTierImage request, CancellationToken cancellationToken)
        {
            if (!AllowedContentTypes.Contains(request.File.ContentType))
                throw new PreconditionException(ErrorCode.UnsupportedImageType, "Unsupported image type. Allowed: JPEG, PNG, WebP, GIF.");

            if (request.File.Length > MaxFileSizeBytes)
                throw new PreconditionException(ErrorCode.FileTooLarge, "File is too large. Maximum allowed size is 5 MB.");

            // Verify tier exists for this club
            var tierExists = await context.BadgeTiers.AnyAsync(
                bt => bt.Id == request.TierId && bt.ClubId == request.ClubId, cancellationToken);
            if (!tierExists)
                throw new PreconditionException(ErrorCode.BadgeTierNotFound, "Badge tier not found.");

            await using var inputStream = request.File.OpenReadStream();
            using var ms = new MemoryStream();
            await inputStream.CopyToAsync(ms, cancellationToken);

            using var original = SKBitmap.Decode(ms.ToArray())
                ?? throw new PreconditionException(ErrorCode.CouldNotDecodeImage, "Could not decode the uploaded image.");

            var (targetWidth, targetHeight) = ComputeTargetSize(original.Width, original.Height, MaxDimension);
            using var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            var data = encoded.ToArray();

            var existing = await context.BadgeTierImages
                .FirstOrDefaultAsync(i => i.BadgeTierId == request.TierId, cancellationToken);

            if (existing is null)
                context.BadgeTierImages.Add(new BadgeTierImage(request.TierId, data, "image/jpeg"));
            else
                existing.Update(data, "image/jpeg");

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }

        private static (int Width, int Height) ComputeTargetSize(int originalWidth, int originalHeight, int maxDimension)
        {
            if (originalWidth <= maxDimension && originalHeight <= maxDimension)
                return (originalWidth, originalHeight);

            if (originalWidth >= originalHeight)
                return (maxDimension, (int)(originalHeight * (double)maxDimension / originalWidth));

            return ((int)(originalWidth * (double)maxDimension / originalHeight), maxDimension);
        }
    }
}
