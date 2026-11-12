using SkiaSharp;

namespace Bookennis.Api.Infrastructure.Utils;

/// <summary>Decodes uploaded images without letting a small file expand into gigabytes of pixels.</summary>
public static class UploadedImage
{
    /// <summary>
    /// The largest image that is decoded (about 50 megapixels, more than any phone camera). A 5 MB PNG can declare
    /// 20000 × 20000 pixels, which would need 1.6 GB of memory when decoded.
    /// </summary>
    public const long MaxPixels = 50_000_000;

    /// <summary>The decoded image, or <c>null</c> when it is no image or larger than <see cref="MaxPixels"/>.</summary>
    public static SKBitmap? Decode(byte[] data)
    {
        // The codec only reads the header: the size is known before any pixel is decoded
        using var codec = SKCodec.Create(new SKMemoryStream(data));
        if (codec is null)
            return null;

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
            return null;

        return SKBitmap.Decode(codec);
    }
}
