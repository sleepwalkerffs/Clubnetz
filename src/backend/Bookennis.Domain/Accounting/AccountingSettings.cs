using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>
/// The accounting of a club is switched on by setting it up: no row = not set up. Holds what applies to every fiscal year.
/// </summary>
public class AccountingSettings : TenantDomainEntity, IAggregateRoot
{
    private AccountingSettings() { }

    public AccountingSettings(int clubId, int fiscalYearStartMonth)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new PreconditionException(AccountingErrorCode.AccountingFiscalYearStartInvalid, "The first month of the fiscal year is 1-12.");

        ClubId = clubId;
        FiscalYearStartMonth = fiscalYearStartMonth;
    }

    /// <summary>The month every fiscal year starts with (1 = January: the fiscal year is the calendar year). Fixed once set up.</summary>
    public int FiscalYearStartMonth { get; private set; }

    /// <summary>Where the automatic entries put money paid online (the Stripe balance of the club).</summary>
    public int? OnlineMoneyAccountId { get; private set; }

    /// <summary>Where they put bank transfers and where Stripe pays out to.</summary>
    public int? BankMoneyAccountId { get; private set; }

    /// <summary>Where they put cash (membership fees paid in cash, the cash of the clubhouse).</summary>
    public int? CashMoneyAccountId { get; private set; }

    /// <summary>Where they put card payments of the clubhouse (the card terminal or SumUp account of the club).</summary>
    public int? CardMoneyAccountId { get; private set; }

    /// <summary>The automatic entries get German texts ("Mitgliedsbeitrag ..."), else English: the language of who set them up.</summary>
    public bool GermanTexts { get; private set; } = true;

    /// <summary>The money accounts of the automatic entries (checked by the caller: of the club).</summary>
    public void SetAutomationAccounts(int? online, int? bank, int? cash, int? card, bool germanTexts)
    {
        GermanTexts = germanTexts;
        OnlineMoneyAccountId = online;
        BankMoneyAccountId = bank;
        CashMoneyAccountId = cash;
        CardMoneyAccountId = card;
    }
}
