using System.Globalization;
using Bookennis.Domain.Accounting;
using QRCoder;
using SkiaSharp;

namespace Bookennis.Api.Business.Accounting;

/// <summary>What an invoice PDF shows besides the invoice: the club and where to pay.</summary>
public record InvoicePdfData(Invoice Invoice, InvoiceSettings Settings, string ClubName, string Iban);

/// <summary>
/// Draws an invoice as an A4 PDF with SkiaSharp: the club (address, ZVR), the recipient, the lines, the total, the payment details with an
/// EPC QR code ("Zahlen mit Code") and the footer. Drafts and cancelled invoices are marked across the page. The font (Lato) is embedded,
/// so the PDF looks the same on every server.
/// </summary>
public static class InvoicePdf
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Margin = 50f;
    private const float Right = PageWidth - Margin;
    private const float BottomLimit = PageHeight - 110f;

    private static readonly Lazy<SKTypeface> Regular = new(() => LoadFont("Lato-Regular.ttf"));
    private static readonly Lazy<SKTypeface> Bold = new(() => LoadFont("Lato-Bold.ttf"));

    private static readonly SKColor Ink = new(0x1F, 0x29, 0x37);
    private static readonly SKColor Muted = new(0x6B, 0x72, 0x80);
    private static readonly SKColor Line = new(0xD1, 0xD5, 0xDB);
    private static readonly SKColor Accent = new(0x05, 0x96, 0x69);

    private sealed record Texts(
        string Invoice, string Number, string Date, string Due, string ServicePeriod, string Pos, string Description, string Quantity, string UnitPrice,
        string Amount, string Total, string PayTitle, string PayText, string Reference, string QrCaption, string Draft, string Cancelled, string Paid, string Page, string Zvr);

    private static readonly Texts German = new(
        "Rechnung", "Rechnungsnummer", "Rechnungsdatum", "Zahlbar bis", "Leistungszeitraum", "Pos.", "Beschreibung", "Menge", "Einzelpreis",
        "Betrag", "Gesamtbetrag", "Zahlung", "Bitte überweise den Gesamtbetrag bis {0} auf folgendes Konto:", "Verwendungszweck",
        "Zahlen mit Code: mit der Banking-App scannen", "ENTWURF", "STORNIERT", "BEZAHLT", "Seite {0} von {1}", "ZVR-Zahl");

    private static readonly Texts English = new(
        "Invoice", "Invoice number", "Invoice date", "Due", "Service period", "No.", "Description", "Qty", "Unit price",
        "Amount", "Total", "Payment", "Please transfer the total by {0} to this account:", "Reference",
        "Scan with your banking app to pay", "DRAFT", "CANCELLED", "PAID", "Page {0} of {1}", "ZVR");

    public static byte[] Render(InvoicePdfData data)
    {
        var invoice = data.Invoice;
        var texts = invoice.German ? German : English;
        var culture = CultureInfo.GetCultureInfo(invoice.German ? "de-AT" : "en-GB");

        using var stream = new MemoryStream();
        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{texts.Invoice} {invoice.Number ?? texts.Draft}",
            Author = data.ClubName,
            Creator = "Clubnetz",
            Producer = "Clubnetz",
            Creation = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            RasterDpi = 300,
            EncodingQuality = 95,
            PdfA = false,
        };

        // Two passes: the first counts the pages for "page 1 of 2"
        var pageCount = Draw(null, data, texts, culture, 0);
        using (var document = SKDocument.CreatePdf(stream, metadata))
            Draw(document, data, texts, culture, pageCount);

        return stream.ToArray();
    }

    private static int Draw(SKDocument? document, InvoicePdfData data, Texts texts, CultureInfo culture, int pageCount)
    {
        var invoice = data.Invoice;
        var settings = data.Settings;
        var page = 1;
        using var recorder = document is null ? new SKPictureRecorder() : null;
        var canvas = document?.BeginPage(PageWidth, PageHeight) ?? recorder!.BeginRecording(new SKRect(0, 0, PageWidth, PageHeight));

        string Money(long cents) => (cents / 100m).ToString("C", culture);
        string Day(DateOnly date) => date.ToString(culture.Name == "de-AT" ? "dd.MM.yyyy" : "d MMM yyyy", culture);

        // ---------- head: the club on the left, the invoice details on the right ----------
        var y = Margin + 10;
        Text(canvas, data.ClubName, Margin, y, 16, bold: true, Accent);
        y += 18;
        foreach (var line in settings.Address.Split('\n'))
        {
            Text(canvas, line, Margin, y, 9, color: Muted);
            y += 12;
        }
        if (settings.Contact is { } contact)
        {
            Text(canvas, contact, Margin, y, 9, color: Muted);
            y += 12;
        }

        var detailY = Margin + 10;
        Text(canvas, texts.Invoice.ToUpper(culture), Right, detailY, 20, bold: true, Ink, SKTextAlign.Right);
        detailY += 22;
        void Detail(string label, string value)
        {
            Text(canvas, label, Right - 110, detailY, 9, color: Muted, align: SKTextAlign.Right);
            Text(canvas, value, Right, detailY, 9, bold: true, Ink, SKTextAlign.Right);
            detailY += 13;
        }
        Detail(texts.Number, invoice.Number ?? "—");
        Detail(texts.Date, Day(invoice.InvoiceDate));
        Detail(texts.Due, Day(invoice.DueDate));
        if (invoice.ServicePeriod is { } period)
            Detail(texts.ServicePeriod, period);
        if (settings.ZvrNumber is { } zvr)
            Detail(texts.Zvr, zvr);

        // ---------- recipient ----------
        y = Math.Max(y, detailY) + 28;
        Text(canvas, invoice.RecipientName, Margin, y, 11, bold: true, Ink);
        y += 14;
        foreach (var line in (invoice.RecipientAddress ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Text(canvas, line, Margin, y, 10, color: Ink);
            y += 13;
        }

        // ---------- subject and notes ----------
        y += 22;
        if (invoice.Subject is { } subject)
        {
            Text(canvas, subject, Margin, y, 13, bold: true, Ink);
            y += 18;
        }
        if (invoice.Notes is { } notes)
        {
            foreach (var paragraph in notes.Split('\n'))
            {
                foreach (var line in Wrap(paragraph, Right - Margin, 10, bold: false))
                {
                    Text(canvas, line, Margin, y, 10, color: Ink);
                    y += 13;
                }
            }
            y += 6;
        }

        // ---------- lines ----------
        const float colPos = Margin;
        const float colText = Margin + 30;
        const float colQty = Right - 190;
        const float colUnit = Right - 95;
        const float colAmount = Right;
        void Header()
        {
            y += 8;
            Text(canvas, texts.Pos, colPos, y, 8.5f, bold: true, Muted);
            Text(canvas, texts.Description, colText, y, 8.5f, bold: true, Muted);
            Text(canvas, texts.Quantity, colQty, y, 8.5f, bold: true, Muted, SKTextAlign.Right);
            Text(canvas, texts.UnitPrice, colUnit, y, 8.5f, bold: true, Muted, SKTextAlign.Right);
            Text(canvas, texts.Amount, colAmount, y, 8.5f, bold: true, Muted, SKTextAlign.Right);
            y += 6;
            Rule(canvas, y);
            y += 14;
        }

        void NewPage()
        {
            Footer(canvas, settings, texts, page, pageCount, invoice);
            Stamp(canvas, invoice, texts);
            if (document is not null)
                document.EndPage();
            else
                recorder!.EndRecording().Dispose();
            page++;
            canvas = document?.BeginPage(PageWidth, PageHeight) ?? recorder!.BeginRecording(new SKRect(0, 0, PageWidth, PageHeight));
            y = Margin + 10;
            Text(canvas, $"{texts.Invoice} {invoice.Number ?? ""}", Margin, y, 10, bold: true, Muted);
            y += 10;
            Header();
        }

        Header();
        foreach (var line in invoice.Lines.OrderBy(l => l.Position))
        {
            var wrapped = Wrap(line.Text, colQty - colText - 40, 10, bold: false);
            if (y + wrapped.Count * 13 > BottomLimit)
                NewPage();

            Text(canvas, line.Position.ToString(CultureInfo.InvariantCulture), colPos, y, 10, color: Muted);
            var lineY = y;
            foreach (var part in wrapped)
            {
                Text(canvas, part, colText, lineY, 10, color: Ink);
                lineY += 13;
            }
            Text(canvas, line.Quantity.ToString(line.Quantity == decimal.Truncate(line.Quantity) ? "0" : "0.##", culture), colQty, y, 10, color: Ink, align: SKTextAlign.Right);
            Text(canvas, Money(line.UnitPriceCents), colUnit, y, 10, color: Ink, align: SKTextAlign.Right);
            Text(canvas, Money(line.TotalCents), colAmount, y, 10, color: Ink, align: SKTextAlign.Right);
            y = lineY + 5;
        }

        Rule(canvas, y - 4);
        y += 12;
        Text(canvas, texts.Total, colUnit, y, 12, bold: true, Ink, SKTextAlign.Right);
        Text(canvas, Money(invoice.TotalCents), colAmount, y, 12, bold: true, Ink, SKTextAlign.Right);
        y += 30;

        // ---------- payment with the QR code ----------
        const float qrSize = 92f;
        if (y + qrSize + 20 > BottomLimit)
            NewPage();

        var boxTop = y - 14;
        using (var box = new SKPaint { Color = new SKColor(0xF3, 0xF4, 0xF6), IsAntialias = true })
            canvas.DrawRoundRect(new SKRect(Margin, boxTop, Right, boxTop + qrSize + 24), 8, 8, box);

        var textX = Margin + 14;
        y += 2;
        Text(canvas, texts.PayTitle, textX, y, 11, bold: true, Ink);
        y += 16;
        foreach (var part in Wrap(string.Format(culture, texts.PayText, Day(invoice.DueDate)), Right - Margin - qrSize - 50, 9.5f, bold: false))
        {
            Text(canvas, part, textX, y, 9.5f, color: Ink);
            y += 13;
        }
        y += 3;
        void PayLine(string label, string value)
        {
            Text(canvas, label, textX, y, 9.5f, color: Muted);
            Text(canvas, value, textX + 95, y, 9.5f, bold: true, Ink);
            y += 13;
        }
        PayLine(culture.Name == "de-AT" ? "Empfänger" : "Beneficiary", data.ClubName);
        PayLine("IBAN", FormatIban(data.Iban));
        if (settings.Bic is { } bic)
            PayLine("BIC", bic);
        PayLine(texts.Reference, invoice.Number ?? "—");

        if (invoice.Number is { } number && invoice.Status == InvoiceStatus.Issued)
        {
            var payload = EpcQrPayload.For(data.ClubName, data.Iban, settings.Bic, invoice.TotalCents, number);
            DrawQr(canvas, payload, Right - qrSize - 12, boxTop + 8, qrSize);
            Text(canvas, texts.QrCaption, Right - qrSize / 2 - 12, boxTop + qrSize + 18, 6.5f, color: Muted, align: SKTextAlign.Center);
        }

        Footer(canvas, settings, texts, page, pageCount, invoice);
        Stamp(canvas, invoice, texts);
        if (document is not null)
        {
            document.EndPage();
            document.Close();
        }
        else
            recorder!.EndRecording().Dispose();

        return page;
    }

    private static void Footer(SKCanvas canvas, InvoiceSettings settings, Texts texts, int page, int pageCount, Invoice invoice)
    {
        var y = PageHeight - 72;
        Rule(canvas, y - 12);
        if (settings.Footer is { } footer)
        {
            foreach (var paragraph in footer.Split('\n'))
            {
                foreach (var line in Wrap(paragraph, Right - Margin, 8, bold: false))
                {
                    Text(canvas, line, Margin, y, 8, color: Muted);
                    y += 10;
                }
            }
        }
        if (pageCount > 1)
            Text(canvas, string.Format(CultureInfo.InvariantCulture, texts.Page, page, pageCount), Right, PageHeight - 30, 8, color: Muted, align: SKTextAlign.Right);
    }

    /// <summary>Drafts, cancelled and paid invoices say so across the page: a draft is never mistaken for the real one.</summary>
    private static void Stamp(SKCanvas canvas, Invoice invoice, Texts texts)
    {
        var (text, color) = invoice.Status switch
        {
            InvoiceStatus.Draft => (texts.Draft, new SKColor(0xDC, 0x26, 0x26, 0x40)),
            InvoiceStatus.Cancelled => (texts.Cancelled, new SKColor(0xDC, 0x26, 0x26, 0x50)),
            InvoiceStatus.Paid => (texts.Paid, new SKColor(0x05, 0x96, 0x69, 0x40)),
            _ => (null, SKColors.Transparent),
        };
        if (text is null)
            return;

        canvas.Save();
        canvas.RotateDegrees(-30, PageWidth / 2, PageHeight / 2);
        Text(canvas, text, PageWidth / 2, PageHeight / 2 + 30, 90, bold: true, color, SKTextAlign.Center);
        canvas.Restore();
    }

    private static void DrawQr(SKCanvas canvas, string payload, float x, float y, float size)
    {
        using var generator = new QRCodeGenerator();
        using var code = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M, forceUtf8: true);
        var matrix = code.ModuleMatrix;
        var cell = size / matrix.Count;
        using var white = new SKPaint { Color = SKColors.White };
        using var black = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        canvas.DrawRect(new SKRect(x, y, x + size, y + size), white);
        for (var row = 0; row < matrix.Count; row++)
        {
            for (var column = 0; column < matrix[row].Count; column++)
            {
                if (matrix[row][column])
                    canvas.DrawRect(new SKRect(x + column * cell, y + row * cell, x + (column + 1) * cell + 0.2f, y + (row + 1) * cell + 0.2f), black);
            }
        }
    }

    private static void Text(SKCanvas canvas, string text, float x, float y, float size, bool bold = false, SKColor? color = null, SKTextAlign align = SKTextAlign.Left)
    {
        using var font = new SKFont(bold ? Bold.Value : Regular.Value, size);
        using var paint = new SKPaint { Color = color ?? Ink, IsAntialias = true };
        canvas.DrawText(text, x, y, align, font, paint);
    }

    private static void Rule(SKCanvas canvas, float y)
    {
        using var paint = new SKPaint { Color = Line, StrokeWidth = 0.8f, IsAntialias = true };
        canvas.DrawLine(Margin, y, Right, y, paint);
    }

    /// <summary>Breaks <paramref name="text"/> into lines that fit <paramref name="width"/> (long words are cut).</summary>
    private static List<string> Wrap(string text, float width, float size, bool bold)
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

    private static string FormatIban(string iban)
    {
        var compact = iban.Replace(" ", "", StringComparison.Ordinal);
        return string.Join(' ', Enumerable.Range(0, (compact.Length + 3) / 4).Select(i => compact.Substring(i * 4, Math.Min(4, compact.Length - i * 4))));
    }

    private static SKTypeface LoadFont(string name)
    {
        using var stream = typeof(InvoicePdf).Assembly.GetManifestResourceStream($"Bookennis.Api.Resources.Fonts.{name}")
                           ?? throw new InvalidOperationException($"The font {name} is not embedded.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data) ?? throw new InvalidOperationException($"The font {name} could not be read.");
    }
}
