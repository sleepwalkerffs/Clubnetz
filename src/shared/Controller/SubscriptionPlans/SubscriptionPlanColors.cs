namespace Bookennis.Shared.Controller.SubscriptionPlans;

/// <summary>Participant colors shared by the web app and the Excel export.</summary>
public static class SubscriptionPlanColors
{
    public record ParticipantColor(string Background, string Text);

    public static IReadOnlyList<ParticipantColor> Palette { get; } =
    [
        new("#2563EB", "#FFFFFF"), // blue
        new("#DC2626", "#FFFFFF"), // red
        new("#16A34A", "#FFFFFF"), // green
        new("#F59E0B", "#1F2937"), // amber
        new("#9333EA", "#FFFFFF"), // purple
        new("#0891B2", "#FFFFFF"), // cyan
        new("#DB2777", "#FFFFFF"), // pink
        new("#65A30D", "#FFFFFF"), // lime
        new("#EA580C", "#FFFFFF"), // orange
        new("#4F46E5", "#FFFFFF"), // indigo
        new("#0D9488", "#FFFFFF"), // teal
        new("#FACC15", "#1F2937"), // yellow
        new("#78350F", "#FFFFFF"), // brown
        new("#64748B", "#FFFFFF"), // slate
        new("#F472B6", "#1F2937"), // light pink
        new("#1E3A8A", "#FFFFFF"), // navy
    ];

    public static ParticipantColor Get(int colorIndex)
        => Palette[((colorIndex % Palette.Count) + Palette.Count) % Palette.Count];
}
