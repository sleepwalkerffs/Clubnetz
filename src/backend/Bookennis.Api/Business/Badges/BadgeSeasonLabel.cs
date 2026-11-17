using System.Globalization;
using Bookennis.Shared.Utils;

namespace Bookennis.Api.Business.Badges;

/// <summary>The label of the season of a badge: "01.04.2026 - 31.10.2026", in the United States "4/1/2026 - 10/31/2026".</summary>
public static class BadgeSeasonLabel
{
    /// <summary><paramref name="culture"/> is the club's culture (<see cref="RegionalCulture"/>); a plain culture writes it like an Austrian club.</summary>
    public static string Of(DateOnly from, DateOnly to, CultureInfo culture)
    {
        var regional = RegionalCulture.Of(culture);
        return $"{regional.Date(from)} - {regional.Date(to)}";
    }
}
