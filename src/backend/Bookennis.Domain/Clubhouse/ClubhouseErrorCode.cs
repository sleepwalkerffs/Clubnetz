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
}
