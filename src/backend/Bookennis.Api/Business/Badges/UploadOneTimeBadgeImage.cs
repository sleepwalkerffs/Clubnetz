using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Bookennis.Api.Business.Badges;

public record UploadOneTimeBadgeImage(int ClubId, int OneTimeBadgeId, IFormFile File) : ICommand
{
    public enum ErrorCode
    {
        UnsupportedImageType = 0,
        FileTooLarge = 1,
        OneTimeBadgeNotFound = 2,
        CouldNotDecodeImage = 3
    }

    private static readonly HashSet<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int MaxDimension = 128;

    public class Handler(AppDbContext context) : IRequestHandler<UploadOneTimeBadgeImage>
    {
        public async Task<Unit> Handle(UploadOneTimeBadgeImage request, CancellationToken cancellationToken)
        {
            if (!AllowedContentTypes.Contains(request.File.ContentType))
                throw new PreconditionException(ErrorCode.UnsupportedImageType, "Unsupported image type. Allowed: JPEG, PNG, WebP, GIF.");

            if (request.File.Length > MaxFileSizeBytes)
                throw new PreconditionException(ErrorCode.FileTooLarge, "File is too large. Maximum allowed size is 5 MB.");

            var badgeExists = await context.OneTimeBadges.AnyAsync(
                b => b.Id == request.OneTimeBadgeId && b.ClubId == request.ClubId, cancellationToken);
            if (!badgeExists)
                throw new PreconditionException(ErrorCode.OneTimeBadgeNotFound, "One-time badge not found.");

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

            var existing = await context.OneTimeBadgeImages
                .FirstOrDefaultAsync(i => i.OneTimeBadgeId == request.OneTimeBadgeId, cancellationToken);

            if (existing is null)
                context.OneTimeBadgeImages.Add(new OneTimeBadgeImage(request.OneTimeBadgeId, data, "image/jpeg"));
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
