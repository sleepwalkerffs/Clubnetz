namespace Bookennis.Domain.Clubhouse;

/// <summary>
/// The rules of the clubhouse tabs that are broken by invalid input. The names are the error codes the client localizes
/// (<c>BackendErrorCodeLocale</c>), so never rename or renumber them.
/// </summary>
public enum ClubhouseErrorCode
{
    /// <summary>Details: the maximum length.</summary>
    ClubhouseItemNameInvalid = 0,

    /// <summary>Details: the highest price in cents.</summary>
    ClubhouseItemPriceInvalid = 1,

    /// <summary>A tab is either for a member of the club or for a guest with a name, never both or neither. Details: the maximum length of the name.</summary>
    ClubhouseTabGuestInvalid = 2,

    /// <summary>The tab was already paid: nothing can be added, removed or paid again.</summary>
    ClubhouseTabNotOpen = 3,

    /// <summary>Details: the highest quantity of one entry.</summary>
    ClubhouseQuantityInvalid = 4,

    /// <summary>Details: the maximum number of entries of a tab.</summary>
    ClubhouseTabTooManyLines = 5,

    /// <summary>A tab without entries can't be paid, only discarded.</summary>
    ClubhouseTabEmpty = 6,

    /// <summary>Only an empty tab can be discarded: remove its entries first or let the guest pay.</summary>
    ClubhouseTabNotEmpty = 7,

    /// <summary>The item is not on the menu any more (hidden).</summary>
    ClubhouseItemNotAvailable = 8,

    /// <summary>The member is paying the tab online right now: it is locked until the payment succeeds, fails or expires (at most 30 minutes).</summary>
    ClubhouseTabPaymentRunning = 9,

    /// <summary>The host has not connected a SumUp account (or it has to be connected again): tabs can't be paid in the app.</summary>
    ClubhouseOnlinePaymentNotAvailable = 10,

    /// <summary>The tab of a guest from outside can't be paid in the app.</summary>
    ClubhouseTabNotPayableOnline = 11,

    /// <summary>SumUp declined the request or could not be reached. Details: the message of SumUp.</summary>
    ClubhouseSumUpUnavailable = 12,

    /// <summary>Connecting the SumUp account failed or was cancelled at SumUp, or the link was too old.</summary>
    ClubhouseSumUpConnectFailed = 13,

    /// <summary>The SumUp account can't be disconnected while a member is paying a tab with it.</summary>
    ClubhouseSumUpPaymentsRunning = 14,
}
