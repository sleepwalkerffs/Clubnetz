namespace Bookennis.Domain.Accounting;

/// <summary>
/// What a tennis club starts with when it sets up its accounting: the usual income and expense categories and two money accounts. The
/// treasurer changes, adds and archives them afterwards. Names in German or English, by the language of the treasurer who sets it up.
/// A club in Austria (and Germany, Switzerland) gets the chart with the tax spheres of §§ 34 ff. BAO; a club in the United States a simple
/// chart of a non-profit tennis club (dues, court fees, USTA ...), all in <see cref="TaxSphere.NonProfit"/>: the spheres are not shown there.
/// </summary>
public static class AccountingTemplate
{
    private sealed record Category(AccountingCategoryKind Kind, string Number, string German, string English, TaxSphere Sphere);

    private static readonly Category[] Categories =
    [
        new(AccountingCategoryKind.Income, "4000", "Mitgliedsbeiträge", "Membership fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4010", "Beitrittsgebühren", "Joining fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4100", "Platzgebühren und Gastspieler", "Court fees and guest players", TaxSphere.AuxiliaryOperation),
        new(AccountingCategoryKind.Income, "4110", "Training und Kurse", "Coaching and courses", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4200", "Turniere und Nenngelder", "Tournaments and entry fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4300", "Kantine und Clubhaus", "Canteen and clubhouse", TaxSphere.AuxiliaryOperation),
        new(AccountingCategoryKind.Income, "4310", "Vereinsfeste", "Club parties", TaxSphere.AuxiliaryOperation),
        new(AccountingCategoryKind.Income, "4400", "Sponsoring und Werbung", "Sponsoring and advertising", TaxSphere.CommercialOperation),
        new(AccountingCategoryKind.Income, "4500", "Subventionen und Förderungen", "Subsidies and grants", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4510", "Spenden", "Donations", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4600", "Zinserträge", "Interest", TaxSphere.AssetManagement),
        new(AccountingCategoryKind.Income, "4610", "Miet- und Pachteinnahmen", "Rent received", TaxSphere.AssetManagement),
        new(AccountingCategoryKind.Income, "4900", "Sonstige Einnahmen", "Other income", TaxSphere.NonProfit),

        new(AccountingCategoryKind.Expense, "5000", "Verbandsbeiträge (ÖTV, Landesverband)", "Association fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5010", "Meisterschafts- und Nenngebühren", "League and entry fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5100", "Platzpflege und Material", "Court maintenance and material", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5110", "Bälle und Sportgeräte", "Balls and equipment", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5200", "Trainerhonorare", "Coaches", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5300", "Energie (Strom, Wasser, Gas)", "Energy (electricity, water, gas)", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5310", "Pacht und Miete", "Rent and lease", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5400", "Instandhaltung und Reparaturen", "Repairs and upkeep", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5500", "Versicherungen", "Insurance", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5600", "Kantine und Clubhaus Wareneinkauf", "Canteen and clubhouse purchases", TaxSphere.AuxiliaryOperation),
        new(AccountingCategoryKind.Expense, "5610", "Vereinsfeste Aufwand", "Club party costs", TaxSphere.AuxiliaryOperation),
        new(AccountingCategoryKind.Expense, "5700", "Büro, Porto und Software", "Office, postage and software", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5710", "Bankspesen und Zahlungsgebühren", "Bank and payment fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5800", "Jugendarbeit", "Youth work", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5900", "Sonstige Ausgaben", "Other expenses", TaxSphere.NonProfit),
    ];

    /// <summary>A US non-profit tennis club: no tax spheres (all <see cref="TaxSphere.NonProfit"/>), numbers in the style of a simple US chart of accounts.</summary>
    private static readonly Category[] UnitedStatesCategories =
    [
        new(AccountingCategoryKind.Income, "4000", "Mitgliedsbeiträge", "Membership dues", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4010", "Aufnahmegebühren", "Initiation fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4100", "Platzgebühren und Gastspieler", "Court fees and guest fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4200", "Training und Kurse", "Lessons and clinics", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4300", "Turniere und Veranstaltungen", "Tournaments and events", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4400", "Clubhaus und Pro-Shop", "Clubhouse and pro shop", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4500", "Spenden", "Donations", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4510", "Sponsoring", "Sponsorships", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4520", "Zuschüsse", "Grants", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4600", "Zinserträge", "Interest income", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Income, "4900", "Sonstige Einnahmen", "Other income", TaxSphere.NonProfit),

        new(AccountingCategoryKind.Expense, "5000", "Platzpflege und Reparaturen", "Court maintenance and repairs", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5010", "Platzbeleuchtung", "Court lighting", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5100", "Strom, Wasser, Gas", "Utilities", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5110", "Miete und Pacht", "Rent and lease", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5200", "Versicherungen", "Insurance", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5300", "USTA- und Ligagebühren", "USTA and league fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5310", "Turnier- und Startgebühren", "Tournament and entry fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5400", "Trainer", "Coaching and pros", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5500", "Bälle und Ausrüstung", "Balls and equipment", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5600", "Veranstaltungen und Verpflegung", "Events and food", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5610", "Clubhaus und Pro-Shop Einkauf", "Clubhouse and pro shop purchases", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5700", "Büro, Porto und Software", "Office, postage and software", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5710", "Bank- und Zahlungsgebühren", "Bank and payment fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5800", "Jugendprogramm", "Junior program", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5810", "Steuerberatung und Gebühren", "Accounting, tax and filing fees", TaxSphere.NonProfit),
        new(AccountingCategoryKind.Expense, "5900", "Sonstige Ausgaben", "Other expenses", TaxSphere.NonProfit),
    ];

    public static IEnumerable<AccountingCategory> CreateCategories(int clubId, bool german, bool unitedStates = false)
        => (unitedStates ? UnitedStatesCategories : Categories)
            .Select(c => new AccountingCategory(clubId, new AccountingCategory.CategoryData(c.Kind, c.Number, german ? c.German : c.English, c.Sphere)));

    public static IEnumerable<MoneyAccount> CreateMoneyAccounts(int clubId, bool german, bool unitedStates = false)
    {
        yield return new MoneyAccount(clubId, new MoneyAccount.AccountData(german ? "Girokonto" : unitedStates ? "Checking account" : "Bank account", MoneyAccountKind.Bank, null, 0));
        yield return new MoneyAccount(clubId, new MoneyAccount.AccountData(german ? "Kassa" : unitedStates ? "Cash" : "Cash box", MoneyAccountKind.Cash, null, 1));
    }
}
