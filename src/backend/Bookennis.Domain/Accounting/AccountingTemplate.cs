namespace Bookennis.Domain.Accounting;

/// <summary>
/// What a tennis club in Austria starts with when it sets up its accounting: the usual income and expense categories with their tax
/// sphere and two money accounts. The treasurer changes, adds and archives them afterwards. Names in German or English, by the
/// language of the treasurer who sets it up.
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

    public static IEnumerable<AccountingCategory> CreateCategories(int clubId, bool german)
        => Categories.Select(c => new AccountingCategory(clubId, new AccountingCategory.CategoryData(c.Kind, c.Number, german ? c.German : c.English, c.Sphere)));

    public static IEnumerable<MoneyAccount> CreateMoneyAccounts(int clubId, bool german)
    {
        yield return new MoneyAccount(clubId, new MoneyAccount.AccountData(german ? "Girokonto" : "Bank account", MoneyAccountKind.Bank, null, 0));
        yield return new MoneyAccount(clubId, new MoneyAccount.AccountData(german ? "Kassa" : "Cash box", MoneyAccountKind.Cash, null, 1));
    }
}
