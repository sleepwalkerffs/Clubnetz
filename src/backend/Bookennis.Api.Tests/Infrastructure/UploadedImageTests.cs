using System.Buffers.Binary;
using System.Text;
using Bookennis.Api.Infrastructure.Utils;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

public class UploadedImageTests
{
    [Fact]
    public void NormalImage_IsDecoded()
    {
        using var bitmap = new SKBitmap(40, 30);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        using var decoded = UploadedImage.Decode(png.ToArray());

        decoded.Should().NotBeNull();
        decoded!.Width.Should().Be(40);
    }

    [Fact]
    public void TinyFileDeclaringAHugeImage_IsNotDecoded()
    {
        // A valid PNG header that declares 20000 × 20000 pixels (1.6 GB decoded) in a file of a few bytes
        var png = PngHeader(20_000, 20_000);

        var decoded = UploadedImage.Decode(png);

        decoded.Should().BeNull();
    }

    [Fact]
    public void NoImage_IsNotDecoded() => UploadedImage.Decode(Encoding.UTF8.GetBytes("not an image")).Should().BeNull();

    private static byte[] PngHeader(int width, int height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA

        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "IHDR", ihdr);
        WriteChunk(stream, "IDAT", [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01]);
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(typeAndData);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeAndData));
        stream.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }
}
