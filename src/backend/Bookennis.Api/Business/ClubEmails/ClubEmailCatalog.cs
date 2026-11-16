using System.Globalization;
using System.Reflection;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Bookennis.Shared.Utils;
using Fluid;

namespace Bookennis.Api.Business.ClubEmails;

public record ClubEmailVariable(string Name, string SampleValue);

/// <remarks><c>Sample</c>: Sample variables in a language, with dates, times and amounts written like the club (<see cref="RegionalCulture"/>).</remarks>
public record ClubEmailDefinition(ClubEmailType Type, Type VariablesType, Func<Language, RegionalCulture, ClubEmailVariables> Sample)
{
    /// <summary>All variable paths usable in the template (e.g. <c>member.first_name</c>).</summary>
    public IReadOnlyList<string> VariablePaths { get; } = ClubEmailCatalog.GetVariablePaths(VariablesType).ToList();

    /// <summary>Sample variables in <paramref name="language"/>, written like the club (<paramref name="culture"/>; an Austrian club when <c>null</c>).</summary>
    public ClubEmailVariables CreateSample(Language language, RegionalCulture? culture = null)
        => Sample(language, culture ?? RegionalCulture.For(language == Language.German, global::Bookennis.Shared.Controller.Shared.Country.Austria));

    public IReadOnlyList<ClubEmailVariable> GetVariables(Language language, ClubVariables club, RegionalCulture? culture = null)
    {
        var sample = CreateSample(language, culture) with { Club = club };
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
            (language, culture) => new WelcomeEmailVariables(SampleMember, SampleSeason(culture))),
        new(ClubEmailType.SeasonActivated, typeof(SeasonActivatedEmailVariables),
            (language, culture) => new SeasonActivatedEmailVariables(SampleMember, SampleSeason(culture))),
        new(ClubEmailType.BadgeAwarded, typeof(BadgeAwardedEmailVariables),
            (language, culture) => new BadgeAwardedEmailVariables(
                SampleMember,
                new BadgeVariables(
                    "Gold",
                    language == Language.German ? "20 Matches in einer Saison gespielt" : "Played 20 matches in one season",
                    null,
                    false),
                "https://www.example.com/trophy-case")),
        new(ClubEmailType.BookingDeleted, typeof(BookingDeletedEmailVariables),
            (language, culture) => new BookingDeletedEmailVariables(
                SampleMember,
                new BookingVariables(
                    culture.DateAndTime(new DateTime(2025, 6, 14, 18, 0, 0)),
                    "1",
                    "Erika Muster",
                    language == Language.German ? "gelöscht da der Platz gesperrt wurde" : "setting the court inactive"))),
        new(ClubEmailType.Announcement, typeof(AnnouncementEmailVariables),
            (language, culture) => new AnnouncementEmailVariables(
                SampleMember,
                new AnnouncementVariables(
                    language == Language.German ? "Sommerfest am 12. Juli" : "Summer party on July 12",
                    new MarkdownText(language == Language.German
                        ? "Wir laden alle Mitglieder herzlich zu unserem **Sommerfest** ein.\n\n- Beginn: 17:00 Uhr\n- Für Essen und Getränke ist gesorgt"
                        : "All members are warmly invited to our **summer party**.\n\n- Start: 5 pm\n- Food and drinks are provided"),
                    "https://www.example.com/clubs/1/news/1"))),
        new(ClubEmailType.BookingAdded, typeof(BookingAddedEmailVariables),
            (language, culture) => new BookingAddedEmailVariables(SampleMember, SampleBooking(culture), "Erika Muster")),
        new(ClubEmailType.BookingReminder, typeof(BookingReminderEmailVariables),
            (language, culture) => new BookingReminderEmailVariables(SampleMember, SampleBooking(culture))),
        new(ClubEmailType.ClubEventCreated, typeof(ClubEventEmailVariables),
            (language, culture) => new ClubEventEmailVariables(SampleMember, SampleEvent(culture))),
        new(ClubEmailType.ClubEventReminder, typeof(ClubEventEmailVariables),
            (language, culture) => new ClubEventEmailVariables(SampleMember, SampleEvent(culture))),
        new(ClubEmailType.ClubEventRegistrationDeadline, typeof(ClubEventEmailVariables),
            (language, culture) => new ClubEventEmailVariables(SampleMember, SampleEvent(culture))),
        new(ClubEmailType.MembershipPaymentReceived, typeof(MembershipEmailVariables),
            (language, culture) => new MembershipEmailVariables(SampleMember, SampleMembership(culture, paid: true))),
        new(ClubEmailType.MembershipApproved, typeof(MembershipEmailVariables),
            (language, culture) => new MembershipEmailVariables(SampleMember, SampleMembership(culture, paid: false))),
        new(ClubEmailType.MembershipPaymentFailed, typeof(MembershipEmailVariables),
            (language, culture) => new MembershipEmailVariables(SampleMember, SampleMembership(culture, paid: false))),
        new(ClubEmailType.MembershipNeedsAttention, typeof(MembershipNeedsAttentionEmailVariables),
            (language, culture) => new MembershipNeedsAttentionEmailVariables(
                SampleMember,
                SampleMembership(culture, paid: false),
                language == Language.German ? "Ein Mitglied möchte einen Tarif, den ihr bestätigen müsst." : "A member asks for a plan that you have to confirm.",
                "Erika Muster")),
        new(ClubEmailType.CourtPurchaseConfirmed, typeof(CourtPurchaseEmailVariables),
            (language, culture) => new CourtPurchaseEmailVariables(SampleMember, SampleCourtPurchase(culture))),
        new(ClubEmailType.CourtPurchaseFailed, typeof(CourtPurchaseEmailVariables),
            (language, culture) => new CourtPurchaseEmailVariables(SampleMember, SampleCourtPurchase(culture))),
        new(ClubEmailType.CourtPurchaseNeedsAttention, typeof(CourtPurchaseNeedsAttentionEmailVariables),
            (language, culture) => new CourtPurchaseNeedsAttentionEmailVariables(
                SampleMember,
                SampleCourtPurchase(culture),
                language == Language.German
                    ? "Die Zahlung wurde in Stripe erstattet. Die Buchung wurde entfernt, falls sie noch nicht vorbei war."
                    : "The payment was refunded in Stripe. The booking was removed if it was not over yet.",
                "Erika Muster")),
        new(ClubEmailType.CourtPurchaseCompensated, typeof(CourtPurchaseCompensatedEmailVariables),
            (language, culture) => new CourtPurchaseCompensatedEmailVariables(
                SampleMember,
                SampleCourtPurchase(culture),
                language == Language.German ? "Der Platz wird für ein Training gebraucht." : "The court is needed for a training.",
                language == Language.German ? "1 Buchungsguthaben" : "1 booking credit")),
        new(ClubEmailType.ClubhouseTabPaid, typeof(ClubhouseTabPaidEmailVariables),
            (language, culture) => new ClubhouseTabPaidEmailVariables(SampleMember, SampleClubhouseTab(culture))),
        new(ClubEmailType.ClubhouseTabNeedsAttention, typeof(ClubhouseTabNeedsAttentionEmailVariables),
            (language, culture) => new ClubhouseTabNeedsAttentionEmailVariables(
                SampleMember,
                SampleClubhouseTab(culture),
                language == Language.German
                    ? "Die Rechnung war schon bezahlt, als die Online-Zahlung ankam. Bitte erstatte eine der beiden Zahlungen."
                    : "The tab was already paid when the online payment arrived. Please refund one of the two payments.",
                "Erika Muster")),
        new(ClubEmailType.Invoice, typeof(InvoiceEmailVariables),
            (language, culture) => new InvoiceEmailVariables(SampleMember, SampleInvoice(language, 0))),
        new(ClubEmailType.InvoiceReminder, typeof(InvoiceEmailVariables),
            (language, culture) => new InvoiceEmailVariables(SampleMember, SampleInvoice(language, 1)))
    ];

    private static InvoiceVariables SampleInvoice(Language language, int reminders)
    {
        var culture = language.ToCultureInfo();
        return new InvoiceVariables("RE2026-0007", 450m.ToString("C", culture), new DateOnly(2026, 5, 4).ToString("d", culture), new DateOnly(2026, 5, 18).ToString("d", culture),
            language == Language.German ? "Bandenwerbung Saison 2026" : "Advertising board season 2026", "AT61 1904 3002 3457 3201", reminders);
    }

    public static ClubEmailDefinition Get(ClubEmailType type)
        => All.SingleOrDefault(d => d.Type == type) ?? throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown club email type.");

    private static MemberVariables SampleMember => new("Max", "Muster");

    private static BookingDetailsVariables SampleBooking(RegionalCulture culture)
        => new(
            new DateOnly(2025, 6, 14).ToString("D", culture),
            $"{culture.Time(new TimeOnly(18, 0))} – {culture.Time(new TimeOnly(19, 0))}",
            "1",
            culture.IsGerman ? "Einzel" : "Singles",
            "Max Muster, Erika Muster",
            "https://www.example.com/clubs/1/booking/1");

    private static EventVariables SampleEvent(RegionalCulture culture)
    {
        return new EventVariables(
            culture.IsGerman ? "Sommerfest" : "Summer party",
            new DateOnly(2025, 7, 12).ToString("D", culture),
            $"{culture.Time(new TimeOnly(17, 0))} – {culture.Time(new TimeOnly(22, 0))}",
            culture.IsGerman ? "Clubhaus" : "Clubhouse",
            $"{new DateOnly(2025, 7, 5).ToString("D", culture)}, {culture.Time(new TimeOnly(18, 0))}",
            "https://www.example.com/clubs/1/calendar/1");
    }

    private static MembershipVariables SampleMembership(RegionalCulture culture, bool paid)
    {
        var language = culture.IsGerman ? Language.German : Language.English;
        return new MembershipVariables(
            language == Language.German ? "Familie" : "Family",
            SampleSeason(culture).Period,
            Memberships.MembershipTexts.Amount(21500, culture),
            "Max Muster, Erika Muster, Mia Muster",
            paid ? (language == Language.German ? "Online-Zahlung" : "Online payment") : "",
            "https://www.example.com/clubs/1/membership");
    }

    private static ClubhouseTabVariables SampleClubhouseTab(RegionalCulture culture)
    {
        var language = culture.IsGerman ? Language.German : Language.English;
        return new ClubhouseTabVariables(
            Memberships.MembershipTexts.Amount(1650, culture),
            new DateOnly(2025, 6, 14).ToString("D", culture),
            language == Language.German ? "2× Bier, 1× Schnitzel" : "2× Beer, 1× Schnitzel",
            "TEENSK4W2K",
            "https://www.example.com/clubs/1/my-club");
    }

    private static CourtPurchaseVariables SampleCourtPurchase(RegionalCulture culture)
    {
        var language = culture.IsGerman ? Language.German : Language.English;
        var price = Memberships.MembershipTexts.Amount(2800, culture);
        return new CourtPurchaseVariables(
            language == Language.German ? "Platz 2" : "Court 2",
            new DateOnly(2025, 6, 14).ToString("D", culture),
            $"{culture.Time(new TimeOnly(17, 0))} – {culture.Time(new TimeOnly(18, 0))}",
            price,
            language == Language.German ? $"{price} online" : $"{price} online",
            "https://www.example.com/clubs/1/booking");
    }

    private static SeasonVariables SampleSeason(RegionalCulture culture)
    {
        return FormatSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 10, 31), culture);
    }

    /// <summary>The period of a season in the short date of the club's culture: "01.04.2025" or "4/1/2025".</summary>
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
