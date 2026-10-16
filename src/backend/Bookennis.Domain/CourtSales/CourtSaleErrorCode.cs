namespace Bookennis.Domain.CourtSales;

/// <summary>
/// The rules of court sales that are broken by invalid input. The names are the error codes the client localizes
/// (<c>BackendErrorCodeLocale</c>), so never rename or renumber them.
/// </summary>
public enum CourtSaleErrorCode
{
    /// <summary>Selling courts can only be switched on with a play mode for the bought bookings.</summary>
    CourtSalePlayModeRequired = 0,

    CourtSaleInvalidTimeZone = 1,

    /// <summary>Details: the smallest and the largest allowed value.</summary>
    CourtSaleInvalidMaxBookingMinutes = 2,

    /// <summary>Details: the smallest and the largest allowed value.</summary>
    CourtSaleInvalidMaxDaysInAdvance = 3,

    /// <summary>Details: the smallest and the largest allowed value.</summary>
    CourtSaleInvalidMaxUpcomingBookings = 4,

    CourtPriceRuleWeekdayRequired = 5,

    /// <summary>The start is not before the end, or a time is not on a full or half hour.</summary>
    CourtPriceRuleInvalidTime = 6,

    CourtPriceRuleInvalidPrice = 7,

    /// <summary>Details: the maximum length.</summary>
    CourtPriceRuleNameTooLong = 8,

    /// <summary>Two rules share a weekday and a court and their times intersect. Details: name of the other rule (may be empty), its start and end (HH:mm).</summary>
    CourtPriceRulesOverlap = 9,

    /// <summary>The purchase is in a state that does not allow the action. Details: the status.</summary>
    CourtPurchaseInvalidStatus = 10,

    /// <summary>A grant of 0 credits.</summary>
    BookingCreditCountInvalid = 11,

    /// <summary>Details: the maximum length.</summary>
    BookingCreditNoteTooLong = 12,

    /// <summary>The play mode for bought slots can overbook: it would delete the bookings of others.</summary>
    CourtSalePlayModeCanOverbook = 13,

    /// <summary>The play mode for bought slots has a fixed duration: the buyer chooses the length.</summary>
    CourtSalePlayModeHasFixedDuration = 14,
}
