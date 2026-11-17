using Bookennis.Client.Localization.Components.Sports;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Components.Sports;

public static class SportNames
{
    /// <summary>The name of the sport in the language of the user ("Tennis", "Padel").</summary>
    public static string Name(IStringLocalizer<SportLocale> locale, Sport sport) => locale[$"Sport_{sport}"].Value;

    /// <summary>The name of a tier badge with the sport of its track ("Gold · Padel"); the name alone for every sport and one-time badges.</summary>
    public static string BadgeName(IStringLocalizer<SportLocale> locale, string name, Sport? sport)
        => sport is { } s ? $"{name} · {Name(locale, s)}" : name;

    /// <summary>Emoji of the sport, for chips and labels.</summary>
    public static string Emoji(Sport sport) => sport switch
    {
        Sport.Padel => "🏓",
        _ => "🎾",
    };
}
