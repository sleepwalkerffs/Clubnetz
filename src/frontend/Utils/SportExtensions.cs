using Bookennis.Client.Localization;
using Bookennis.Shared.Controller.Club;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Utils;

/// <summary>
/// Sports of courts, enrollments and membership plans. Lists of sports are empty for "every sport".
/// A club whose courts all have the same sport sees nothing of it: check <see cref="HasSeveralSports"/> before showing sport UI.
/// </summary>
public static class SportExtensions
{
    public static readonly Sport[] All = Enum.GetValues<Sport>();

    /// <summary>The club's courts are for more than one sport (tennis and padel): only then the app shows sports.</summary>
    public static bool HasSeveralSports(this IEnumerable<CourtResult> courts) => courts.Select(c => c.Sport).Distinct().Skip(1).Any();

    /// <summary>The list covers the sport; an empty (or missing) list covers every sport.</summary>
    public static bool Covers(this IReadOnlyCollection<Sport>? sports, Sport sport) => sports is null || sports.Count == 0 || sports.Contains(sport);

    public static string Emoji(this Sport sport) => sport switch
    {
        Sport.Padel => "🏓",
        _ => "🎾",
    };

    public static string Name(this Sport sport, IStringLocalizer<SharedLocale> locale) => locale[$"Sport_{sport}"];

    /// <summary>"🎾 Tennis"</summary>
    public static string Label(this Sport sport, IStringLocalizer<SharedLocale> locale) => $"{sport.Emoji()} {sport.Name(locale)}";

    /// <summary>"Tennis, Padel", or "Every sport" for an empty list.</summary>
    public static string Names(this IReadOnlyCollection<Sport>? sports, IStringLocalizer<SharedLocale> locale)
        => sports is null || sports.Count == 0 || All.All(sports.Contains)
            ? locale["Sports_Every"]
            : string.Join(", ", sports.Distinct().Order().Select(s => s.Name(locale)));

    /// <summary>The sports for saving: ordered, and empty when every sport is ticked (the same as none).</summary>
    public static List<Sport> Normalize(this IEnumerable<Sport> sports)
    {
        var list = sports.Distinct().Order().ToList();
        return All.All(list.Contains) ? [] : list;
    }
}
