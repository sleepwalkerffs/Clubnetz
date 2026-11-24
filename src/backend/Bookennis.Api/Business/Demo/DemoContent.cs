using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;

namespace Bookennis.Api.Business.Demo;

/// <summary>The people visitors log in as. The FE describes each one with its own text (<c>DemoLocale</c>).</summary>
public enum DemoPersona
{
    Admin,
    Member,
    Parent,
    Trainer,
    Treasurer,
    ClubhouseHost,
    TwoClubs,
}

/// <summary>Everything that is written in the language of one demo club: names, courts, events, news, the menu of the clubhouse.</summary>
public sealed record DemoClubContent
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required Country Country { get; init; }
    public required Language Language { get; init; }
    public required string TimeZoneId { get; init; }
    public required string Currency { get; init; }
    public required decimal Latitude { get; init; }
    public required decimal Longitude { get; init; }
    public required string City { get; init; }
    public required string ZipCode { get; init; }
    public required string Street { get; init; }
    public required string WebsiteUrl { get; init; }

    public required DemoCourt[] Courts { get; init; }
    public required DemoPlayModeNames PlayModes { get; init; }

    /// <summary>The accounts on the login page. <see cref="DemoPerson.Persona"/> is set for each of them.</summary>
    public required DemoPerson[] Personas { get; init; }

    /// <summary>The family of the <see cref="DemoPersona.Parent"/>: a second parent and two children.</summary>
    public required DemoPerson SecondParent { get; init; }

    public required DemoPerson[] Children { get; init; }

    public required string[] MaleFirstNames { get; init; }
    public required string[] FemaleFirstNames { get; init; }
    public required string[] LastNames { get; init; }

    public required string[] BadgeTierNames { get; init; }

    /// <summary>Description of a tier, <c>{0}</c> = the matches it needs.</summary>
    public required string BadgeTierDescription { get; init; }

    public required DemoOneTimeBadge[] OneTimeBadges { get; init; }
    public required DemoEvent[] Events { get; init; }
    public required DemoAnnouncement[] Announcements { get; init; }
    public required DemoPlan[] Plans { get; init; }
    public required DemoItem[] ClubhouseItems { get; init; }
    public required string[] WalkInGuests { get; init; }

    public required string WinterBreakTitle { get; init; }
    public required string KidsTrainingComment { get; init; }
    public required string TeamTrainingComment { get; init; }
    public required string PadelMeetupComment { get; init; }
    public required string[] BookingComments { get; init; }
    public required string PlayRequestComment { get; init; }
    public required string OtherPlayRequestComment { get; init; }
    public required string PlayerGroupName { get; init; }
    public required string[] MembershipNotes { get; init; }

    /// <summary>Court sales: weekday daytime, weekday evening, weekend.</summary>
    public required string[] PriceRuleNames { get; init; }

    public required int[] PriceRuleCentsPerHour { get; init; }

    /// <summary>Text of the income entry of a paid membership, <c>{0}</c> = the plan, <c>{1}</c> = the payer.</summary>
    public required string MembershipFeeText { get; init; }

    public required string MembershipFeeCategory { get; init; }
    public required DemoAccountingEntry[] AccountingEntries { get; init; }
}

/// <summary>
/// An entry of the accounting in the running year: once on <see cref="Month"/>/<see cref="Day"/>, or every month on <see cref="Day"/>
/// when <see cref="Month"/> is <c>null</c>. Only entries up to today are made. <see cref="Category"/> is the number of the category in
/// <c>AccountingTemplate</c>.
/// </summary>
public sealed record DemoAccountingEntry(string Text, string Category, int AmountCents, int? Month, int Day, bool Cash = false, string? Counterparty = null);

public sealed record DemoCourt(string Name, string Alias, Sport Sport, bool IsIndoor);

public sealed record DemoPlayModeNames(string Singles, string Doubles, string Padel, string Training, string TeamMatch);

public sealed record DemoPerson(string FirstName, string LastName, Gender Gender, DateOnly Birthday, MemberRole[] Roles, DemoPersona? Persona = null)
{
    public string Email => DemoAccounts.EmailFor(FirstName, LastName);
}

public sealed record DemoOneTimeBadge(string Name, string Description, bool LastSeason, DemoPersona? AwardedTo, int AwardedToOthers);

public sealed record DemoQuestion(string Text, ClubEventQuestionSelectionMode Mode, bool IsRequired, bool AllowQuantities, string[] Options);

/// <summary>An event <see cref="DayOffset"/> days from today (negative = in the past).</summary>
public sealed record DemoEvent(
    string Title,
    string Description,
    string? Location,
    ClubEventCategory Category,
    int DayOffset,
    int? Days,
    TimeOnly? Start,
    TimeOnly? End,
    bool Registration,
    int? MaxParticipants,
    int? DeadlineDaysBefore,
    DemoQuestion[] Questions,
    int Registrations,
    string[] RegistrationComments);

/// <summary>A news item; <c>{0}</c> in the title is next year.</summary>
public sealed record DemoAnnouncement(string Title, string Body, bool IsPinned, int DaysAgo, int? ExpiresInDays);

public sealed record DemoPlan(
    string Name,
    string Description,
    int PriceCents,
    int MaxAdults,
    bool IncludesChildren,
    int? MaxAge,
    bool RequiresApproval,
    bool NewMembersOnly,
    bool IsSelfService,
    Sport[]? Sports);

public sealed record DemoItem(string Name, ClubhouseItemCategory Category, int PriceCents);

public static class DemoContent
{
    public static readonly DemoClubContent Europe = new()
    {
        Key = "eu",
        Name = "TC Lindenau",
        Country = Country.Austria,
        Language = Language.German,
        TimeZoneId = "Europe/Vienna",
        Currency = "eur",
        Latitude = 48.2082m,
        Longitude = 16.3738m,
        City = "Lindenau",
        ZipCode = "1234",
        Street = "Sportplatzweg 7",
        WebsiteUrl = "https://clubnetz.app",
        Courts =
        [
            new("Platz 1", "P1", Sport.Tennis, false),
            new("Platz 2", "P2", Sport.Tennis, false),
            new("Platz 3", "P3", Sport.Tennis, false),
            new("Platz 4", "P4", Sport.Tennis, false),
            new("Halle 1", "H1", Sport.Tennis, true),
            new("Halle 2", "H2", Sport.Tennis, true),
            new("Padel 1", "PA1", Sport.Padel, true),
            new("Padel 2", "PA2", Sport.Padel, true),
        ],
        PlayModes = new("Einzel", "Doppel", "Padel", "Training", "Meisterschaft"),
        Personas =
        [
            new("Katharina", "Berger", Gender.Female, new(1979, 3, 14), [MemberRole.User, MemberRole.Admin], DemoPersona.Admin),
            new("Lukas", "Steiner", Gender.Male, new(1991, 8, 2), [MemberRole.User], DemoPersona.Member),
            new("Sabine", "Huber", Gender.Female, new(1983, 11, 21), [MemberRole.User], DemoPersona.Parent),
            new("Marco", "Gruber", Gender.Male, new(1988, 5, 9), [MemberRole.User, MemberRole.Trainer, MemberRole.YouthSportsDirector], DemoPersona.Trainer),
            new("Thomas", "Wallner", Gender.Male, new(1969, 1, 30), [MemberRole.User, MemberRole.Treasurer], DemoPersona.Treasurer),
            new("Gerhard", "Moser", Gender.Male, new(1965, 7, 17), [MemberRole.ClubhouseHost], DemoPersona.ClubhouseHost),
            new("Julia", "Weber", Gender.Female, new(1994, 4, 26), [MemberRole.User], DemoPersona.TwoClubs),
        ],
        SecondParent = new("Andreas", "Huber", Gender.Male, new(1981, 2, 3), [MemberRole.User]),
        Children =
        [
            new("Emma", "Huber", Gender.Female, new(2012, 6, 12), [MemberRole.User]),
            new("Paul", "Huber", Gender.Male, new(2015, 9, 28), [MemberRole.User]),
        ],
        MaleFirstNames = ["Stefan", "Michael", "Florian", "Christoph", "Markus", "Daniel", "Martin", "Johannes", "Philipp", "Sebastian", "Alexander", "Matthias", "Bernhard", "Wolfgang", "Dominik", "Tobias", "Manuel", "Patrick"],
        FemaleFirstNames = ["Lisa", "Anna", "Elisabeth", "Sarah", "Christina", "Barbara", "Verena", "Claudia", "Nina", "Theresa", "Magdalena", "Eva", "Johanna", "Melanie", "Petra", "Andrea"],
        LastNames = ["Bauer", "Wagner", "Pichler", "Mayer", "Hofer", "Leitner", "Fuchs", "Eder", "Fischer", "Schmid", "Winkler", "Schwarz", "Maier", "Reiter", "Brunner", "Lang", "Baumgartner", "Auer", "Wimmer", "Egger", "Lehner", "Haas", "Koller", "Wieser"],
        BadgeTierNames = ["Bronze", "Silber", "Gold", "Platin"],
        BadgeTierDescription = "{0} Spiele in der Saison",
        OneTimeBadges =
        [
            new("Clubmeister", "Sieger der Clubmeisterschaft im Einzel", true, DemoPersona.Member, 0),
            new("Helfer des Jahres", "Beim Arbeitseinsatz im Frühjahr immer dabei", true, DemoPersona.Parent, 2),
            new("Teamgeist", "Für den Einsatz in der Mannschaft", false, null, 4),
        ],
        Events =
        [
            new("Clubmeisterschaft – Finaltag", "Die Finalspiele im Einzel und Doppel, danach Siegerehrung auf der Terrasse.", "Platz 1", ClubEventCategory.Tournament, -75, null, new(10, 0), new(18, 0), false, null, null, [], 0, []),
            new("Saisonabschluss & Heuriger", "Wir lassen die Freiluftsaison gemütlich ausklingen. Bitte bis eine Woche vorher anmelden, damit wir genug bestellen können.", "Clubhaus", ClubEventCategory.Social, -40, null, new(17, 0), new(23, 0), true, 80, 7,
                [new("Essen", ClubEventQuestionSelectionMode.SingleChoice, true, true, ["Schweinsbraten", "Käsespätzle (vegetarisch)", "Kein Essen"])], 22, ["Wir bringen einen Kuchen mit!", ""]),
            new("Weihnachtsfeier", "Punsch, Kekse und ein Rückblick auf die Saison mit Fotos. Kinder sind herzlich willkommen – der Nikolaus schaut vorbei!", "Clubhaus", ClubEventCategory.Social, 18, null, new(18, 30), new(23, 0), true, 70, 3,
                [
                    new("Essen", ClubEventQuestionSelectionMode.SingleChoice, true, true, ["Gulasch", "Gemüsestrudel (vegetarisch)", "Kein Essen"]),
                    new("Ich helfe mit beim", ClubEventQuestionSelectionMode.MultipleChoice, false, false, ["Aufbau", "Punschstand", "Abbau"]),
                ], 16, ["Wir kommen zu viert, zwei Kinder", "Ich bringe Kekse mit", ""]),
            new("Hallenturnier Mixed-Doppel", "Spaßturnier in der Halle, die Paarungen werden ausgelost. Jede und jeder spielt mindestens drei Matches.", "Halle 1 & 2", ClubEventCategory.Tournament, 32, null, new(9, 0), new(17, 0), true, 24, 5, [], 11, ["Spiele gerne mit Lisa", ""]),
            new("Jahreshauptversammlung", "Tagesordnung: Berichte des Vorstands und des Kassiers, Entlastung, Neuwahlen, Allfälliges. Anträge bitte bis zwei Wochen vorher an den Vorstand.", "Clubhaus", ClubEventCategory.Meeting, 60, null, new(19, 0), new(21, 30), true, null, 1, [], 9, []),
            new("Arbeitseinsatz Platzinstandsetzung", "Wir machen die Freiplätze fit für die Saison. Bitte Arbeitshandschuhe mitbringen, für Jause ist gesorgt.", "Freiplätze", ClubEventCategory.WorkEffort, 125, null, new(8, 0), new(13, 0), true, 30, 3,
                [new("Ich kann", ClubEventQuestionSelectionMode.SingleChoice, true, false, ["den ganzen Vormittag", "ab 10 Uhr", "nur bis 10 Uhr"])], 6, [""]),
            new("Tenniscamp Kroatien", "Eine Woche Training und Meer in Umag. Infos zu Anreise und Unterkunft folgen per Mail.", "Umag, Kroatien", ClubEventCategory.Other, 150, 7, null, null, true, 16, 30, [], 5, ["Mit Partner, der nicht spielt"]),
        ],
        Announcements =
        [
            new("Hallensaison hat begonnen", "Die Freiplätze sind bis Ende März im **Winterschlaf**. Gespielt wird jetzt in **Halle 1 und 2** und auf den beiden Padel-Plätzen.\n\nBitte denkt an saubere Hallenschuhe. Wer noch keine Hallenstunde hat, findet im Buchungsplan freie Zeiten am Vormittag und am Wochenende.", true, 20, null),
            new("Padel-Schnupperabende", "Jeden Freitag ab 18 Uhr treffen sich Padel-Neulinge auf **Padel 1**. Schläger und Bälle sind vorhanden, einfach vorbeikommen!\n\nAnmeldung über die *Spielersuche* in der App.", false, 9, 60),
            new("Neue Mitgliedsbeiträge für {0}", "Die Jahreshauptversammlung hat die Beiträge für die nächste Saison beschlossen. Die Übersicht findet ihr unter *Mitgliedschaft*. Familien zahlen weiterhin einen Pauschalbetrag.", false, 3, null),
            new("Danke an alle Helferinnen und Helfer!", "Beim Saisonabschluss haben über 20 Mitglieder mitgeholfen. Ein besonderer Dank geht an das Team vom Clubhaus für den Schweinsbraten.", false, 38, 30),
        ],
        Plans =
        [
            new("Erwachsene", "Freies Spielen auf allen Plätzen für eine Person ab 19 Jahren.", 26000, 1, false, null, false, false, true, null),
            new("Paar", "Zwei Erwachsene im gleichen Haushalt.", 44000, 2, false, null, false, false, true, null),
            new("Familie", "Zwei Erwachsene und alle Kinder bis 18 Jahre.", 52000, 2, true, null, false, false, true, null),
            new("Jugend", "Kinder und Jugendliche bis 18 Jahre, inklusive Kindertraining.", 9000, 1, false, 18, false, false, true, null),
            new("Nur Padel", "Freies Spielen auf den Padel-Plätzen.", 18000, 1, false, null, false, false, true, [Sport.Padel]),
            new("Schnuppermitgliedschaft", "Ein Jahr zum halben Preis für neue Mitglieder. Der Vorstand bestätigt die Anmeldung.", 13000, 1, false, null, true, true, true, null),
        ],
        ClubhouseItems =
        [
            new("Bier 0,5 l", ClubhouseItemCategory.Drink, 450),
            new("Radler 0,5 l", ClubhouseItemCategory.Drink, 420),
            new("Weißer Spritzer", ClubhouseItemCategory.Drink, 350),
            new("Almdudler", ClubhouseItemCategory.Drink, 320),
            new("Mineral 0,5 l", ClubhouseItemCategory.Drink, 250),
            new("Kaffee", ClubhouseItemCategory.Drink, 280),
            new("Schinken-Käse-Toast", ClubhouseItemCategory.Food, 450),
            new("Frankfurter mit Senf", ClubhouseItemCategory.Food, 520),
            new("Schnitzelsemmel", ClubhouseItemCategory.Food, 680),
            new("Apfelstrudel", ClubhouseItemCategory.Food, 380),
            new("Dose Tennisbälle", ClubhouseItemCategory.Other, 990),
        ],
        WalkInGuests = ["Tisch 3", "Gäste vom TC Nachbarort"],
        WinterBreakTitle = "Winterpause Freiplätze",
        KidsTrainingComment = "Kindertraining",
        TeamTrainingComment = "Mannschaftstraining Herren",
        PadelMeetupComment = "Padel-Schnupperabend",
        BookingComments = ["Revanche!", "Trainingsmatch", "Mit Ballmaschine", "Doppel-Runde"],
        PlayRequestComment = "Wer hat Lust auf ein Doppel nach der Arbeit?",
        OtherPlayRequestComment = "Suche einen Gegner für ein lockeres Einzel",
        PlayerGroupName = "Donnerstagsrunde",
        MembershipNotes = ["Überweisung erhalten", "Bar bei der Weihnachtsfeier bezahlt"],
        PriceRuleNames = ["Tagestarif", "Abendtarif", "Wochenende"],
        PriceRuleCentsPerHour = [1800, 2600, 2200],
        MembershipFeeText = "Mitgliedsbeitrag {0} – {1}",
        MembershipFeeCategory = "4000",
        AccountingEntries =
        [
            new("Strom Flutlicht und Halle", "5300", 68400, null, 5, Counterparty: "Energie Lindenau"),
            new("Trainerhonorar Kindertraining", "5200", 52000, null, 28, Counterparty: "Marco Gruber"),
            new("Pacht Clubhaus", "4610", 45000, null, 3, Counterparty: "Gerhard Moser"),
            new("Verbandsbeitrag Landesverband", "5000", 124000, 1, 20, Counterparty: "Landestennisverband"),
            new("Haftpflicht- und Gebäudeversicherung", "5500", 189000, 2, 1, Counterparty: "Versicherung"),
            new("Tennisbälle (40 Dosen)", "5110", 31600, 3, 12, Counterparty: "Sporthandel"),
            new("Ziegelmehl und Linien Freiplätze", "5100", 284000, 4, 2, Counterparty: "Platzbau"),
            new("Nenngelder Mannschaftsmeisterschaft", "5010", 36000, 4, 15),
            new("Förderung Gemeinde Jugendarbeit", "4500", 150000, 5, 10, Counterparty: "Gemeinde Lindenau"),
            new("Sponsoring Bandenwerbung", "4400", 120000, 6, 1, Counterparty: "Autohaus"),
            new("Clubmeisterschaft Nenngelder", "4200", 46000, 9, 6, Cash: true),
            new("Saisonabschluss Einkauf", "5610", 82000, 10, 10),
            new("Saisonabschluss Einnahmen", "4310", 104000, 10, 12, Cash: true),
            new("Reparatur Netzpfosten", "5400", 21500, 11, 4),
        ],
    };

    public static readonly DemoClubContent UnitedStates = new()
    {
        Key = "us",
        Name = "Lakeview Racquet Club",
        Country = Country.UnitedStates,
        Language = Language.English,
        TimeZoneId = "America/Chicago",
        Currency = "usd",
        Latitude = 41.9400m,
        Longitude = -87.6500m,
        City = "Lakeview",
        ZipCode = "60657",
        Street = "1200 Shoreline Drive",
        WebsiteUrl = "https://clubnetz.app",
        Courts =
        [
            new("Court 1", "C1", Sport.Tennis, false),
            new("Court 2", "C2", Sport.Tennis, false),
            new("Court 3", "C3", Sport.Tennis, true),
            new("Court 4", "C4", Sport.Tennis, true),
            new("Padel A", "PA", Sport.Padel, true),
            new("Padel B", "PB", Sport.Padel, true),
        ],
        PlayModes = new("Singles", "Doubles", "Padel", "Lesson", "League Match"),
        Personas =
        [
            new("Jennifer", "Collins", Gender.Female, new(1977, 10, 5), [MemberRole.User, MemberRole.Admin], DemoPersona.Admin),
            new("Mike", "Johnson", Gender.Male, new(1989, 2, 18), [MemberRole.User], DemoPersona.Member),
            new("Sarah", "Miller", Gender.Female, new(1984, 7, 23), [MemberRole.User], DemoPersona.Parent),
            new("Carlos", "Rivera", Gender.Male, new(1986, 12, 1), [MemberRole.User, MemberRole.Trainer, MemberRole.YouthSportsDirector], DemoPersona.Trainer),
            new("David", "Thompson", Gender.Male, new(1971, 4, 11), [MemberRole.User, MemberRole.Treasurer], DemoPersona.Treasurer),
            new("Tony", "Russo", Gender.Male, new(1968, 9, 8), [MemberRole.ClubhouseHost], DemoPersona.ClubhouseHost),
        ],
        SecondParent = new("Brian", "Miller", Gender.Male, new(1982, 1, 15), [MemberRole.User]),
        Children =
        [
            new("Lily", "Miller", Gender.Female, new(2013, 3, 3), [MemberRole.User]),
            new("Ethan", "Miller", Gender.Male, new(2016, 8, 19), [MemberRole.User]),
        ],
        MaleFirstNames = ["James", "Robert", "Chris", "Daniel", "Matthew", "Kevin", "Ryan", "Jason", "Eric", "Andrew", "Justin", "Steven", "Greg", "Scott"],
        FemaleFirstNames = ["Emily", "Jessica", "Ashley", "Megan", "Lauren", "Rachel", "Amanda", "Nicole", "Stephanie", "Katie", "Heather", "Melissa"],
        LastNames = ["Smith", "Williams", "Brown", "Jones", "Garcia", "Davis", "Martinez", "Anderson", "Taylor", "Moore", "Jackson", "Lee", "White", "Harris", "Clark", "Lewis", "Walker", "Young", "Allen", "Wright"],
        BadgeTierNames = ["Bronze", "Silver", "Gold", "Platinum"],
        BadgeTierDescription = "{0} matches in the season",
        OneTimeBadges =
        [
            new("Club Champion", "Won the club championship singles final", true, DemoPersona.Member, 0),
            new("Volunteer of the Year", "Always there when the club needed a hand", true, DemoPersona.Parent, 1),
            new("Team Spirit", "For the league team", false, null, 3),
        ],
        Events =
        [
            new("Club Championship Finals", "Singles and doubles finals, awards on the patio afterwards.", "Court 1", ClubEventCategory.Tournament, -70, null, new(9, 0), new(17, 0), false, null, null, [], 0, []),
            new("Fall Social & BBQ", "Burgers, music and a look back at the summer season.", "Clubhouse", ClubEventCategory.Social, -35, null, new(16, 0), new(21, 0), true, 90, 5,
                [new("Food", ClubEventQuestionSelectionMode.SingleChoice, true, true, ["Burger", "Veggie burger", "No food"])], 20, ["Bringing dessert!", ""]),
            new("Holiday Party", "Hot cocoa, cookies and the season in photos. Kids welcome!", "Clubhouse", ClubEventCategory.Social, 20, null, new(18, 0), new(22, 0), true, 80, 3,
                [
                    new("Dinner", ClubEventQuestionSelectionMode.SingleChoice, true, true, ["Roast turkey", "Mushroom risotto (vegetarian)", "No dinner"]),
                    new("I can help with", ClubEventQuestionSelectionMode.MultipleChoice, false, false, ["Setup", "Drinks table", "Cleanup"]),
                ], 14, ["Family of four, two kids", ""]),
            new("Winter Doubles Round Robin", "Partners are drawn for every round. Everybody plays at least three matches.", "Courts 3 & 4", ClubEventCategory.Tournament, 30, null, new(9, 0), new(15, 0), true, 24, 4, [], 10, [""]),
            new("Annual Members Meeting", "Board and treasurer reports, budget for next season, board election.", "Clubhouse", ClubEventCategory.Meeting, 55, null, new(19, 0), new(21, 0), true, null, 1, [], 8, []),
            new("Spring Court Cleanup", "We get the outdoor courts ready for the season. Gloves help, lunch is on the club.", "Outdoor courts", ClubEventCategory.WorkEffort, 120, null, new(8, 0), new(12, 0), true, 25, 3,
                [new("I can make it", ClubEventQuestionSelectionMode.SingleChoice, true, false, ["the whole morning", "from 10 am", "until 10 am"])], 5, [""]),
        ],
        Announcements =
        [
            new("Indoor season is on", "Courts 1 and 2 are closed for the winter. Play on **Courts 3 and 4** and on both padel courts.\n\nClean indoor shoes only, please.", true, 18, null),
            new("Padel beginners night", "Every Friday from 6 pm on **Padel A**. Rackets and balls are there, just show up!", false, 8, 60),
            new("{0} membership rates", "The board set the rates for next season. You find them under *Membership*. Family plans stay at a flat rate.", false, 2, null),
        ],
        Plans =
        [
            new("Individual", "Unlimited play for one adult.", 48000, 1, false, null, false, false, true, null),
            new("Couple", "Two adults in the same household.", 82000, 2, false, null, false, false, true, null),
            new("Family", "Two adults and all kids up to 18.", 95000, 2, true, null, false, false, true, null),
            new("Junior", "Kids up to 18, junior clinics included.", 18000, 1, false, 18, false, false, true, null),
            new("Padel only", "Unlimited play on the padel courts.", 32000, 1, false, null, false, false, true, [Sport.Padel]),
        ],
        ClubhouseItems =
        [
            new("Draft beer", ClubhouseItemCategory.Drink, 600),
            new("Lemonade", ClubhouseItemCategory.Drink, 350),
            new("Iced tea", ClubhouseItemCategory.Drink, 350),
            new("Sports drink", ClubhouseItemCategory.Drink, 300),
            new("Coffee", ClubhouseItemCategory.Drink, 250),
            new("Bottled water", ClubhouseItemCategory.Drink, 200),
            new("Club sandwich", ClubhouseItemCategory.Food, 950),
            new("Hot dog", ClubhouseItemCategory.Food, 550),
            new("Protein bar", ClubhouseItemCategory.Food, 350),
            new("Can of balls", ClubhouseItemCategory.Other, 799),
        ],
        WalkInGuests = ["Table 2"],
        WinterBreakTitle = "Outdoor courts closed for winter",
        KidsTrainingComment = "Junior clinic",
        TeamTrainingComment = "League team practice",
        PadelMeetupComment = "Padel beginners night",
        BookingComments = ["Rematch!", "Practice set", "Ball machine", "Doubles night"],
        PlayRequestComment = "Anybody up for doubles after work?",
        OtherPlayRequestComment = "Looking for a hitting partner",
        PlayerGroupName = "Thursday crew",
        MembershipNotes = ["Check received", "Paid at the front desk"],
        PriceRuleNames = ["Daytime", "Evening", "Weekend"],
        PriceRuleCentsPerHour = [3000, 4500, 3800],
        MembershipFeeText = "{0} membership – {1}",
        MembershipFeeCategory = "4000",
        AccountingEntries =
        [
            new("Court lighting and utilities", "5100", 94000, null, 5, Counterparty: "City Power & Light"),
            new("Junior clinic coaching", "5400", 120000, null, 28, Counterparty: "Carlos Rivera"),
            new("USTA league fees", "5300", 85000, 1, 20, Counterparty: "USTA"),
            new("Liability insurance", "5200", 240000, 2, 1, Counterparty: "Insurance"),
            new("Tennis balls (48 cans)", "5500", 26400, 3, 12, Counterparty: "Pro shop supplier"),
            new("Court resurfacing deposit", "5000", 450000, 4, 2, Counterparty: "Court builders"),
            new("Sponsorship – banner", "4510", 150000, 6, 1, Counterparty: "Lakeview Dental"),
            new("Club championship entry fees", "4300", 54000, 9, 6, Cash: true),
            new("Fall social food", "5600", 96000, 10, 10),
            new("Net post repair", "5000", 18500, 11, 4),
        ],
    };

    public static readonly IReadOnlyList<DemoClubContent> Clubs = [Europe, UnitedStates];
}
