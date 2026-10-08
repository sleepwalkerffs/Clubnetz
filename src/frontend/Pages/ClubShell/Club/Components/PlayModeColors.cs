using System.Globalization;

namespace Bookennis.Client.Pages.ClubShell.Club.Components;

public static class PlayModeColors
{
    /// <summary>Suggested play mode colors, readable on the booking grid in light and dark mode.</summary>
    public static readonly string[] Palette =
    [
        "#059669", "#0891B2", "#2563EB", "#7C3AED", "#DB2777", "#DC2626",
        "#EA580C", "#D97706", "#65A30D", "#795548", "#475569", "#0F766E",
    ];

    public static string ToHex(int argb)
    {
        var color = System.Drawing.Color.FromArgb(argb);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static int ToArgb(string hex)
    {
        var value = hex.TrimStart('#');
        var r = int.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return System.Drawing.Color.FromArgb(r, g, b).ToArgb();
    }

    public static string Duration(TimeSpan duration)
        => duration.Minutes == 0
            ? $"{(int)duration.TotalHours} h"
            : $"{(int)duration.TotalHours}:{duration.Minutes:00} h";
}
