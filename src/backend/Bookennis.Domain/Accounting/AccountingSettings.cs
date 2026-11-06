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
}
