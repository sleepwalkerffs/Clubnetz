namespace Bookennis.Domain.Accounting;

/// <summary>
/// The rules of the club's accounting that are broken by invalid input. The names are the error codes the client localizes
/// (<c>BackendErrorCodeLocale</c>), so never rename or renumber them.
/// </summary>
public enum AccountingErrorCode
{
    /// <summary>The treasurer has not set up the accounting of the club yet.</summary>
    AccountingNotSetUp = 0,

    AccountingAlreadySetUp = 1,

    /// <summary>The first month of the fiscal year is 1-12, the first year is not far in the past or the future.</summary>
    AccountingFiscalYearStartInvalid = 2,

    /// <summary>Details: the maximum length.</summary>
    AccountingNameInvalid = 3,

    /// <summary>Another account, category or cost center already has this name. Details: the name.</summary>
    AccountingNameTaken = 4,

    AccountingIbanInvalid = 5,

    /// <summary>Details: the maximum length.</summary>
    AccountingCategoryNumberInvalid = 6,

    /// <summary>The fiscal year is closed: nothing in it can be changed. Details: the label of the year.</summary>
    AccountingFiscalYearClosed = 7,

    /// <summary>No fiscal year contains the date. Details: the date (yyyy-MM-dd).</summary>
    AccountingFiscalYearMissing = 8,

    /// <summary>An entry stays in its fiscal year: its date can't move to another one. Details: the label of the year.</summary>
    AccountingDateOutsideFiscalYear = 9,

    /// <summary>Details: the maximum length.</summary>
    AccountingTextInvalid = 10,

    /// <summary>An amount is 0 or larger than allowed. Details: the largest amount in euros.</summary>
    AccountingAmountInvalid = 11,

    /// <summary>An income or expense needs at least one line. Details: the maximum number of lines.</summary>
    AccountingLinesInvalid = 12,

    /// <summary>A transfer goes from one money account to another one, with a positive amount and no lines.</summary>
    AccountingTransferInvalid = 13,

    /// <summary>The money account, category or cost center is archived and can't be used for new entries. Details: its name.</summary>
    AccountingArchived = 14,

    /// <summary>The entry was cancelled: it can't be changed any more.</summary>
    AccountingEntryCancelled = 15,

    /// <summary>Details: the maximum length of the name.</summary>
    AccountingCounterpartyInvalid = 16,

    /// <summary>A fiscal year is closed after the year before it. Details: the label of that year.</summary>
    AccountingPreviousYearOpen = 17,

    /// <summary>A year can only be opened again while the year after it is open. Details: the label of that year.</summary>
    AccountingNextYearClosed = 18,

    /// <summary>Only a closed fiscal year can be audited or opened again.</summary>
    AccountingFiscalYearNotClosed = 19,

    /// <summary>The opening balances were carried over from the closed year before: change that year instead.</summary>
    AccountingOpeningBalancesCarried = 20,

    /// <summary>It is used by entries or opening balances: archive it instead. Details: its name.</summary>
    AccountingInUse = 21,

    /// <summary>A category that is used can't change from income to expense or back. Details: its name.</summary>
    AccountingCategoryKindInUse = 22,

    /// <summary>Details: the maximum size in MB.</summary>
    AccountingReceiptTooLarge = 23,

    /// <summary>Details: the allowed file types.</summary>
    AccountingReceiptTypeNotAllowed = 24,

    /// <summary>The receipt belongs to another entry. Details: the number of that entry.</summary>
    AccountingReceiptAlreadyBooked = 25,

    /// <summary>Nobody audits the books they keep: the auditor must not also be a treasurer or an admin of the club.</summary>
    AccountingAuditorIsTreasurer = 26,

    /// <summary>Only a member with the role auditor records an audit (not in support mode).</summary>
    AccountingAuditorRequired = 27,

    /// <summary>The next fiscal year can only be created when it starts within a year. Details: the start date (yyyy-MM-dd).</summary>
    AccountingFiscalYearTooFarAhead = 28,

    /// <summary>Details: the maximum length.</summary>
    AccountingCommentInvalid = 29,

    /// <summary>Details: the maximum number of receipts of an entry.</summary>
    AccountingTooManyReceipts = 30,

    /// <summary>A category used in a closed fiscal year keeps its tax sphere: the reports of that year must not change. Details: the name.</summary>
    AccountingCategorySphereInUse = 31,

    /// <summary>A source of automatic entries that is on needs its category and the money accounts its payments go to.</summary>
    AccountingAutomationIncomplete = 32,

    /// <summary>The suggestion was booked or dismissed already.</summary>
    AccountingProposalNotOpen = 33,

    /// <summary>The file is no bank statement that can be read (camt.053, MT940 or CSV), or larger than 5 MB.</summary>
    AccountingBankFileInvalid = 34,

    /// <summary>The bank line was matched or ignored already.</summary>
    AccountingBankTransactionNotOpen = 35,

    /// <summary>The entry does not put the amount of the bank line on its money account. Details: the amount of the line.</summary>
    AccountingBankTransactionAmountMismatch = 36,

    /// <summary>A bank rule needs a text or an IBAN to look for.</summary>
    AccountingBankRuleInvalid = 37,

    /// <summary>The CSV columns don't fit the file: no line could be read with them.</summary>
    AccountingBankCsvLayoutInvalid = 38,

    /// <summary>The entry is matched with a bank line already.</summary>
    AccountingEntryAlreadyMatched = 39,

    /// <summary>The statement is about another bank account. Details: the IBAN of the file.</summary>
    AccountingBankFileOtherAccount = 40,

    /// <summary>The file holds statements of several accounts: give the money account its IBAN, then only its statements are read.</summary>
    AccountingBankFileSeveralAccounts = 52,
}
