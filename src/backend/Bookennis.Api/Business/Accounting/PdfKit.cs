using SkiaSharp;

namespace Bookennis.Api.Business.Accounting;

/// <summary>
/// What the PDFs of the accounting (invoice, annual report) share: an A4 page, the embedded font (Lato, so the PDF looks the same on every server),
/// the colors and drawing text, rules and wrapped lines. Both documents look alike this way.
/// </summary>
internal static class PdfKit
{
    public const float PageWidth = 595f;
    public const float PageHeight = 842f;
    public const float Margin = 50f;
    public const float Right = PageWidth - Margin;

    public static readonly SKColor Ink = new(0x1F, 0x29, 0x37);
    public static readonly SKColor Muted = new(0x6B, 0x72, 0x80);
    public static readonly SKColor Line = new(0xD1, 0xD5, 0xDB);
    public static readonly SKColor Accent = new(0x05, 0x96, 0x69);
    public static readonly SKColor Panel = new(0xF3, 0xF4, 0xF6);

    private static readonly Lazy<SKTypeface> Regular = new(() => LoadFont("Lato-Regular.ttf"));
    private static readonly Lazy<SKTypeface> Bold = new(() => LoadFont("Lato-Bold.ttf"));

    public static SKDocumentPdfMetadata Metadata(string title, string author) => new()
    {
        Title = title,
        Author = author,
        Creator = "Clubnetz",
        Producer = "Clubnetz",
        Creation = DateTime.UtcNow,
        Modified = DateTime.UtcNow,
        RasterDpi = 300,
        EncodingQuality = 95,
        PdfA = false,
    };

    public static void Text(SKCanvas canvas, string text, float x, float y, float size, bool bold = false, SKColor? color = null, SKTextAlign align = SKTextAlign.Left)
    {
        using var font = new SKFont(bold ? Bold.Value : Regular.Value, size);
        using var paint = new SKPaint { Color = color ?? Ink, IsAntialias = true };
        canvas.DrawText(text, x, y, align, font, paint);
    }

    public static float Measure(string text, float size, bool bold)
    {
        using var font = new SKFont(bold ? Bold.Value : Regular.Value, size);
        return font.MeasureText(text);
    }

    public static void Rule(SKCanvas canvas, float y, float left = Margin, float right = Right, SKColor? color = null, float width = 0.8f)
    {
        using var paint = new SKPaint { Color = color ?? Line, StrokeWidth = width, IsAntialias = true };
        canvas.DrawLine(left, y, right, y, paint);
    }

    public static void Box(SKCanvas canvas, SKRect rect, SKColor color, float radius = 8)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    /// <summary>Breaks <paramref name="text"/> into lines that fit <paramref name="width"/> (long words are cut).</summary>
    public static List<string> Wrap(string text, float width, float size, bool bold)
    {
        using var font = new SKFont(bold ? Bold.Value : Regular.Value, size);
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureText(candidate) <= width)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
                lines.Add(current);
            current = word;
            while (font.MeasureText(current) > width && current.Length > 1)
            {
                var cut = current.Length - 1;
                while (cut > 1 && font.MeasureText(current[..cut]) > width)
                    cut--;
                lines.Add(current[..cut]);
                current = current[cut..];
            }
        }

        if (current.Length > 0 || lines.Count == 0)
            lines.Add(current);
        return lines;
    }

    private static SKTypeface LoadFont(string name)
    {
        using var stream = typeof(PdfKit).Assembly.GetManifestResourceStream($"Bookennis.Api.Resources.Fonts.{name}")
                           ?? throw new InvalidOperationException($"The font {name} is not embedded.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data) ?? throw new InvalidOperationException($"The font {name} could not be read.");
    }
}
