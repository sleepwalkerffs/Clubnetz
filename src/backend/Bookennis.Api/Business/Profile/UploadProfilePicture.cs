using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Bookennis.Api.Business.Profile;

public record UploadProfilePicture(IFormFile File) : ICommand
{
    public enum ErrorCode
    {
        UnsupportedImageType = 0,
        FileTooLarge = 1,
        CouldNotDecodeImage = 2
    }

    private static readonly HashSet<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxDimension = 256;

    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<UploadProfilePicture>
    {
        public async Task<Unit> Handle(UploadProfilePicture request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var userId))
                throw new AuthorizationFailedException();

            if (!AllowedContentTypes.Contains(request.File.ContentType))
                throw new PreconditionException(ErrorCode.UnsupportedImageType, "Unsupported image type. Allowed: JPEG, PNG, WebP, GIF.");

            if (request.File.Length > MaxFileSizeBytes)
                throw new PreconditionException(ErrorCode.FileTooLarge, "File is too large. Maximum allowed size is 5 MB.");

            byte[] data;
            await using var inputStream = request.File.OpenReadStream();
            using var ms = new MemoryStream();
            await inputStream.CopyToAsync(ms, cancellationToken);

            using var original = SKBitmap.Decode(ms.ToArray()) ?? throw new PreconditionException(ErrorCode.CouldNotDecodeImage, "Could not decode the uploaded image.");

            var (targetWidth, targetHeight) = ComputeTargetSize(original.Width, original.Height, MaxDimension);
            using var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            data = encoded.ToArray();

            var existing = await context.UserProfilePictures.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (existing is null)
                context.UserProfilePictures.Add(new UserProfilePicture(userId, data, "image/jpeg"));
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
