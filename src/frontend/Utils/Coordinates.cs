using System.Globalization;

namespace Bookennis.Client.Utils;

/// <summary>Reads coordinates as people paste them, e.g. "47.30124, 9.63451" (Google Maps), "47.3012 9.6345" or "47,3012; 9,6345".</summary>
public static class Coordinates
{
    public static bool TryParse(string? text, out decimal latitude, out decimal longitude)
    {
        latitude = longitude = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim().Trim('(', ')');
        string[] parts;
        if (text.Contains(';'))
            parts = text.Split(';');
        else if (text.Count(c => c == ',') == 1 && text.Contains('.'))
            parts = text.Split(',');
        else
            parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
            return false;

        return TryNumber(parts[0], out latitude) && TryNumber(parts[1], out longitude)
               && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    }

    public static string Format(decimal latitude, decimal longitude)
        => string.Create(CultureInfo.InvariantCulture, $"{latitude:0.####}, {longitude:0.####}");

    /// <summary>The spot on OpenStreetMap, to check the coordinates. Only opened when the admin clicks the link.</summary>
    public static string MapUrl(decimal latitude, decimal longitude)
        => string.Create(CultureInfo.InvariantCulture, $"https://www.openstreetmap.org/?mlat={latitude:0.####}&mlon={longitude:0.####}#map=18/{latitude:0.####}/{longitude:0.####}");

    private static bool TryNumber(string text, out decimal value)
        => decimal.TryParse(text.Trim().Replace(',', '.').TrimEnd('°'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
