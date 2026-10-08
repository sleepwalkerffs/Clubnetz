namespace Bookennis.Client.Utils;

public static class BookingColors
{
    /// <summary>Converts a play mode color (ARGB int) into a CSS hex color.</summary>
    public static string ToHex(int colorArgb)
    {
        var color = System.Drawing.Color.FromArgb(colorArgb);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
