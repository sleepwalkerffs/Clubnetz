using System.Globalization;
using System.Reflection;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Fluid;

namespace Bookennis.Api.Business.ClubEmails;

public record ClubEmailVariable(string Name, string SampleValue);

public record ClubEmailDefinition(ClubEmailType Type, Type VariablesType, Func<Language, ClubEmailVariables> CreateSample)
{
    /// <summary>All variable paths usable in the template (e.g. <c>member.first_name</c>).</summary>
    public IReadOnlyList<string> VariablePaths { get; } = ClubEmailCatalog.GetVariablePaths(VariablesType).ToList();

    public IReadOnlyList<ClubEmailVariable> GetVariables(Language language, ClubVariables club)
    {
        var sample = CreateSample(language) with { Club = club };
        return VariablePaths
            .Select(path => new ClubEmailVariable(path, ClubEmailCatalog.GetValue(sample, path)?.ToString() ?? string.Empty))
            .ToList();
    }
}

public static class ClubEmailCatalog
{
    public static ClubVariables SampleClub { get; } = new("TC Example", "https://www.example.com", "office@example.com");

    public static IReadOnlyList<ClubEmailDefinition> All { get; } =
    [
        new(ClubEmailType.Welcome, typeof(WelcomeEmailVariables),
            language => new WelcomeEmailVariables(SampleMember, SampleSeason(language))),
        new(ClubEmailType.SeasonActivated, typeof(SeasonActivatedEmailVariables),
            language => new SeasonActivatedEmailVariables(SampleMember, SampleSeason(language))),
        new(ClubEmailType.BadgeAwarded, typeof(BadgeAwardedEmailVariables),
            language => new BadgeAwardedEmailVariables(
                SampleMember,
                new BadgeVariables(
                    "Gold",
                    language == Language.German ? "20 Matches in einer Saison gespielt" : "Played 20 matches in one season",
                    null,
                    false),
                "https://www.example.com/trophy-case")),
        new(ClubEmailType.GuestCard, typeof(GuestCardEmailVariables),
            _ => new GuestCardEmailVariables(SampleMember, "https://www.example.com/Account/GuestLogin?guestCode=00000000-0000-0000-0000-000000000000")),
        new(ClubEmailType.BookingDeleted, typeof(BookingDeletedEmailVariables),
            language => new BookingDeletedEmailVariables(
                SampleMember,
                new BookingVariables(
                    new DateTime(2025, 6, 14, 18, 0, 0).ToString(language.ToCultureInfo()),
                    "1",
                    "Erika Muster",
                    language == Language.German ? "gelöscht da der Platz gesperrt wurde" : "setting the court inactive"))),
        new(ClubEmailType.Announcement, typeof(AnnouncementEmailVariables),
            language => new AnnouncementEmailVariables(
                SampleMember,
                new AnnouncementVariables(
                    language == Language.German ? "Sommerfest am 12. Juli" : "Summer party on July 12",
                    new MarkdownText(language == Language.German
                        ? "Wir laden alle Mitglieder herzlich zu unserem **Sommerfest** ein.\n\n- Beginn: 17:00 Uhr\n- Für Essen und Getränke ist gesorgt"
                        : "All members are warmly invited to our **summer party**.\n\n- Start: 5 pm\n- Food and drinks are provided"),
                    "https://www.example.com/clubs/1/news/1"))),
        new(ClubEmailType.BookingAdded, typeof(BookingAddedEmailVariables),
            language => new BookingAddedEmailVariables(SampleMember, SampleBooking(language), "Erika Muster")),
        new(ClubEmailType.BookingReminder, typeof(BookingReminderEmailVariables),
            language => new BookingReminderEmailVariables(SampleMember, SampleBooking(language))),
        new(ClubEmailType.ClubEventCreated, typeof(ClubEventEmailVariables),
            language => new ClubEventEmailVariables(SampleMember, SampleEvent(language))),
        new(ClubEmailType.ClubEventReminder, typeof(ClubEventEmailVariables),
            language => new ClubEventEmailVariables(SampleMember, SampleEvent(language))),
        new(ClubEmailType.ClubEventRegistrationDeadline, typeof(ClubEventEmailVariables),
            language => new ClubEventEmailVariables(SampleMember, SampleEvent(language)))
    ];

    public static ClubEmailDefinition Get(ClubEmailType type)
        => All.SingleOrDefault(d => d.Type == type) ?? throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown club email type.");

    private static MemberVariables SampleMember => new("Max", "Muster");

    private static BookingDetailsVariables SampleBooking(Language language)
        => new(
            new DateOnly(2025, 6, 14).ToString("D", language.ToCultureInfo()),
            "18:00 – 19:00",
            "1",
            language == Language.German ? "Einzel" : "Singles",
            "Max Muster, Erika Muster",
            "https://www.example.com/clubs/1/booking/1");

    private static EventVariables SampleEvent(Language language)
    {
        var culture = language.ToCultureInfo();
        return new EventVariables(
            language == Language.German ? "Sommerfest" : "Summer party",
            new DateOnly(2025, 7, 12).ToString("D", culture),
            "17:00 – 22:00",
            language == Language.German ? "Clubhaus" : "Clubhouse",
            $"{new DateOnly(2025, 7, 5).ToString("D", culture)}, 18:00",
            "https://www.example.com/clubs/1/calendar/1");
    }

    private static SeasonVariables SampleSeason(Language language)
    {
        var culture = language.ToCultureInfo();
        return FormatSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 10, 31), culture);
    }

    public static SeasonVariables FormatSeason(DateOnly from, DateOnly to, CultureInfo culture)
        => new(from.ToString(culture.DateTimeFormat.ShortDatePattern, culture), to.ToString(culture.DateTimeFormat.ShortDatePattern, culture));

    /// <summary>The types whose properties may be accessed in templates (everything reachable from the variables).</summary>
    public static IEnumerable<Type> GetExposedTypes()
        => All.SelectMany(d => GetNestedTypes(d.VariablesType)).Distinct();

    public static string GetVariableName(PropertyInfo property) => MemberNameStrategies.SnakeCase(property);

    public static IEnumerable<PropertyInfo> GetExposedProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0);

    internal static IEnumerable<string> GetVariablePaths(Type type, string prefix = "")
    {
        foreach (var property in GetExposedProperties(type).OrderBy(GetSortOrder).ThenBy(p => p.MetadataToken))
        {
            var path = prefix + GetVariableName(property);
            if (IsNested(property.PropertyType))
            {
                foreach (var nested in GetVariablePaths(property.PropertyType, path + "."))
                    yield return nested;
            }
            else
            {
                yield return path;
            }
        }
    }

    internal static object? GetValue(object root, string path)
    {
        object? current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current is null)
                return null;
            var property = GetExposedProperties(current.GetType()).Single(p => GetVariableName(p) == segment);
            current = property.GetValue(current);
        }

        return current;
    }

    private static IEnumerable<Type> GetNestedTypes(Type type)
    {
        yield return type;
        foreach (var property in GetExposedProperties(type).Where(p => IsNested(p.PropertyType)))
        {
            foreach (var nested in GetNestedTypes(property.PropertyType))
                yield return nested;
        }
    }

    private static int GetSortOrder(PropertyInfo property) => property.Name switch
    {
        nameof(ClubEmailVariables.Member) => 0,
        nameof(ClubEmailVariables.Club) => 1,
        _ => 2
    };

    private static bool IsNested(Type type) => type.IsClass && type != typeof(string);
}
