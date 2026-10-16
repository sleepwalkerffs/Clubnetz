using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.CourtSales;

/// <summary>Keep in sync with <c>Bookennis.Domain.CourtSales.CourtPurchaseStatus</c>.</summary>
public enum CourtPurchaseStatus
{
    /// <summary>The slot is held by the booking while the buyer pays online.</summary>
    AwaitingPayment = 0,

    /// <summary>A SEPA direct debit was started and has not settled yet. The booking stays.</summary>
    Processing = 1,
    Paid = 2,

    /// <summary>The buyer did not pay in time: the hold was released and the booking deleted.</summary>
    Expired = 3,
    Failed = 4,

    /// <summary>Cancelled by a treasurer or admin.</summary>
    Cancelled = 5,
    Refunded = 6,
    Disputed = 7,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.CourtSales.CourtPurchasePaymentMethod</c>.</summary>
public enum CourtPurchasePaymentMethod
{
    /// <summary>Paid with Stripe (also the rest of a longer booking that was partly paid with a credit).</summary>
    Online = 0,

    /// <summary>One booking credit paid for all of it.</summary>
    Credit = 1,

    /// <summary>The price was 0.</summary>
    Free = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.CourtSales.BookingCreditReason</c>.</summary>
public enum BookingCreditReason
{
    /// <summary>The bookings left on a guest card of the former guest system.</summary>
    GuestCardMigration = 0,
    GrantedByClub = 1,
}

/// <summary>Why the caller can't buy a slot right now (<see cref="CourtSaleOptionsResult.Reason"/>).</summary>
public enum CourtSaleUnavailableReason
{
    /// <summary>The club does not sell court time (switched off or no play mode chosen).</summary>
    NotEnabled = 0,

    /// <summary>The caller is enrolled in the active season and books for free like every member.</summary>
    AlreadyEnrolled = 1,

    /// <summary>The caller already has <see cref="CourtSaleOptionsResult.MaxUpcomingBookings"/> bought bookings that are not over.</summary>
    LimitReached = 2,

    /// <summary>The caller has no credit and the club can't take online payments (Stripe not connected).</summary>
    PaymentUnavailable = 3,

    /// <summary>Support mode: an administrator without a member in the club can't buy.</summary>
    NoMember = 4,
}

/// <summary>Why a slot can't be bought (<see cref="CourtPurchaseQuoteResult.Reason"/>).</summary>
public enum CourtSaleQuoteUnavailableReason
{
    /// <summary>At least one minute of the slot has no price rule.</summary>
    NotForSale = 0,

    /// <summary>The slot does not start on a full or half hour, or its length is not a multiple of 30 minutes or above the maximum.</summary>
    InvalidDuration = 1,

    /// <summary>The slot is further ahead than <see cref="CourtSaleOptionsResult.MaxDaysInAdvance"/>.</summary>
    TooFarInAdvance = 2,

    /// <summary>The slot has started already.</summary>
    InThePast = 3,

    NotEnabled = 4,
}

// ---------- settings (treasurers) ----------

/// <summary>How the club sells free court time. Read and changed by treasurers and admins.</summary>
public record CourtSaleSettingsDto
{
    public required bool IsEnabled { get; init; }

    /// <summary>The play mode of the bought bookings. Required to switch the sale on.</summary>
    public required int? PlayModeId { get; init; }

    /// <summary>IANA id (e.g. "Europe/Vienna"): the times of the price rules are local times of this zone.</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>30-240, a multiple of 30.</summary>
    public required int MaxBookingMinutes { get; init; }

    /// <summary>0-90.</summary>
    public required int MaxDaysInAdvance { get; init; }

    /// <summary>1-10: bought bookings a member may have that are not over yet.</summary>
    public required int MaxUpcomingBookings { get; init; }

    /// <summary>The club's Stripe account takes payments. Without it only credits and free rules work.</summary>
    public required bool IsOnlinePaymentAvailable { get; init; }
}

/// <summary>Body of <c>PUT CourtSales/settings</c>.</summary>
public record UpdateCourtSaleSettingsModel
{
    public bool IsEnabled { get; init; }
    public int? PlayModeId { get; init; }
    public string? TimeZoneId { get; init; }
    public int MaxBookingMinutes { get; init; }
    public int MaxDaysInAdvance { get; init; }
    public int MaxUpcomingBookings { get; init; }
}

// ---------- price rules ----------

/// <summary>
/// A price per hour on some weekdays between two local times of <see cref="CourtSaleSettingsDto.TimeZoneId"/>.
/// The times are <see cref="TimeSpan"/>s on purpose: the client converts <c>TimeOnly</c> to UTC, but these are local times.
/// </summary>
public record CourtPriceRuleDto
{
    public required int Id { get; init; }

    /// <summary>Empty: all courts of the club.</summary>
    public required List<int> CourtIds { get; init; }

    public required List<DayOfWeek> Weekdays { get; init; }

    /// <summary>Local time of day, on a full or half hour.</summary>
    public required TimeSpan From { get; init; }

    /// <summary>Local time of day, on a full or half hour; 24:00 (1 day) is midnight at the end of the day.</summary>
    public required TimeSpan To { get; init; }

    public required int PricePerHourCents { get; init; }

    /// <summary>Three-letter ISO code in lower case ("eur").</summary>
    public required string Currency { get; init; }

    /// <summary>Optional label, e.g. "Prime time".</summary>
    public string? Name { get; init; }
}

/// <summary>Body of <c>POST CourtSales/price-rules</c> and <c>PUT CourtSales/price-rules/{ruleId}</c>.</summary>
public record SaveCourtPriceRuleModel
{
    /// <summary>Empty or <c>null</c>: all courts.</summary>
    public List<int>? CourtIds { get; init; }

    public List<DayOfWeek>? Weekdays { get; init; }

    /// <summary>Local time of day, on a full or half hour.</summary>
    public TimeSpan From { get; init; }

    /// <summary>Local time of day, on a full or half hour; 24:00 for midnight.</summary>
    public TimeSpan To { get; init; }

    public int PricePerHourCents { get; init; }
    public string? Name { get; init; }
}

// ---------- buying ----------

/// <summary>What the caller can do with court sales: shown on the booking page (<c>GET CourtSales/options</c>).</summary>
public record CourtSaleOptionsResult
{
    /// <summary>The club sells court time.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The caller may buy a slot now. When <c>false</c>, <see cref="Reason"/> says why.</summary>
    public required bool CanBuy { get; init; }

    public CourtSaleUnavailableReason? Reason { get; init; }

    /// <summary>The club's Stripe account takes payments.</summary>
    public required bool IsOnlinePaymentAvailable { get; init; }

    /// <summary>Booking credits of the caller. One credit pays for one booking of up to 60 minutes.</summary>
    public required int Credits { get; init; }

    public required int MaxBookingMinutes { get; init; }
    public required int MaxDaysInAdvance { get; init; }
    public required int MaxUpcomingBookings { get; init; }

    /// <summary>Bought bookings of the caller that are not over (awaiting payment, processing or paid).</summary>
    public required int UpcomingPurchases { get; init; }

    public required int? PlayModeId { get; init; }

    /// <summary>IANA id of the club's time zone for the prices.</summary>
    public required string TimeZoneId { get; init; }

    public required string Currency { get; init; }

    /// <summary>Minutes one credit covers (60).</summary>
    public required int MinutesCoveredByCredit { get; init; }
}

/// <summary>The price of a slot for the caller (<c>GET CourtSales/quote?courtId=&amp;from=&amp;to=</c>).</summary>
public record CourtPurchaseQuoteResult
{
    public required bool IsForSale { get; init; }
    public CourtSaleQuoteUnavailableReason? Reason { get; init; }

    /// <summary>The full price of the slot (0 when not for sale).</summary>
    public required int PriceCents { get; init; }

    /// <summary>A credit of the caller would pay for the slot (or its first hour).</summary>
    public required bool CreditUsed { get; init; }

    /// <summary>What the caller pays online. 0 with a credit for up to an hour or a free rule.</summary>
    public required int AmountDueCents { get; init; }

    public required string Currency { get; init; }
    public required List<CourtQuoteLineDto> Lines { get; init; }
}

/// <summary>A part of the slot with one price per hour. <c>From</c>/<c>To</c> are UTC.</summary>
public record CourtQuoteLineDto
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required int PricePerHourCents { get; init; }
    public string? RuleName { get; init; }
}

/// <summary>Body of <c>POST CourtSales/purchases</c>. The buyer is always the caller and the only player.</summary>
public record StartCourtPurchaseModel
{
    public int CourtId { get; init; }

    /// <summary>Start of the slot, on a full or half hour.</summary>
    public DateTimeOffset From { get; init; }

    /// <summary>End of the slot: a multiple of 30 minutes after <see cref="From"/>, at most <see cref="CourtSaleOptionsResult.MaxBookingMinutes"/>.</summary>
    public DateTimeOffset To { get; init; }
}

public record StartCourtPurchaseResult
{
    public required int PurchaseId { get; init; }
    public required CourtPurchaseStatus Status { get; init; }

    /// <summary>
    /// The payment page: leave the app with <c>NavigateTo(url, forceLoad: true)</c>. <c>null</c> when a credit or a free rule paid for
    /// everything (the purchase is <see cref="CourtPurchaseStatus.Paid"/> and the booking exists).
    /// </summary>
    public string? CheckoutUrl { get; init; }
}

/// <summary>A purchase as its buyer sees it (<c>GET CourtSales/purchases/{purchaseId}</c>, <c>POST .../release</c>).</summary>
public record CourtPurchaseDto
{
    public required int Id { get; init; }
    public required int CourtId { get; init; }
    public required string CourtName { get; init; }

    /// <summary><c>null</c> once the booking was deleted (hold released, cancelled, refunded).</summary>
    public int? BookingId { get; init; }

    public required DateTimeOffsetInterval Interval { get; init; }
    public required int PriceCents { get; init; }
    public required bool CreditUsed { get; init; }
    public required int AmountDueCents { get; init; }
    public required string Currency { get; init; }
    public required CourtPurchaseStatus Status { get; init; }
    public CourtPurchasePaymentMethod? PaymentMethod { get; init; }
    public DateTimeOffset? HoldExpiresAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

// ---------- sales (treasurers) ----------

/// <summary><c>GET CourtSales/purchases?from=&amp;to=&amp;status=</c>: the purchases of the club, newest slot first, and their totals.</summary>
public record GetCourtPurchasesResult
{
    public required List<CourtPurchaseListItemDto> Purchases { get; init; }
    public required CourtPurchasesSummaryDto Summary { get; init; }
}

/// <summary>A purchase in the treasurers' list. Only the name of the buyer, never email or birthday.</summary>
public record CourtPurchaseListItemDto
{
    public required int Id { get; init; }
    public int? MemberId { get; init; }

    /// <summary>Empty strings once the buyer left the club.</summary>
    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    public required int CourtId { get; init; }
    public required string CourtName { get; init; }
    public int? BookingId { get; init; }
    public required DateTimeOffsetInterval Interval { get; init; }
    public required int PriceCents { get; init; }
    public required bool CreditUsed { get; init; }
    public required int AmountDueCents { get; init; }
    public required string Currency { get; init; }
    public required CourtPurchaseStatus Status { get; init; }
    public CourtPurchasePaymentMethod? PaymentMethod { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>A treasurer can cancel it (awaiting payment, processing or paid).</summary>
    public required bool CanCancel { get; init; }

    /// <summary>Money was paid online: after cancelling, refund it in the Stripe dashboard.</summary>
    public required bool HasOnlinePayment { get; init; }
}

/// <summary>Totals of the listed purchases.</summary>
public record CourtPurchasesSummaryDto
{
    /// <summary>Purchases that are paid or processing.</summary>
    public required int SoldCount { get; init; }

    /// <summary>Amount paid online (paid or processing).</summary>
    public required int OnlineAmountCents { get; init; }

    /// <summary>Credits used by paid or processing purchases.</summary>
    public required int CreditsUsed { get; init; }

    /// <summary>Holds that wait for their payment.</summary>
    public required int AwaitingPaymentCount { get; init; }

    /// <summary>Refunded or disputed: worth a look in the Stripe dashboard.</summary>
    public required int NeedsAttentionCount { get; init; }
}

public record CancelCourtPurchaseResult
{
    public required CourtPurchaseListItemDto Purchase { get; init; }

    /// <summary>Money was paid online: tell the treasurer to refund it in the Stripe dashboard (refunds are not made by Clubnetz).</summary>
    public required bool RefundInStripeDashboard { get; init; }
}

// ---------- credits (treasurers) ----------

/// <summary><c>GET CourtSales/credits</c>: every member who ever got a credit.</summary>
public record GetBookingCreditsResult
{
    public required List<BookingCreditBalanceDto> Members { get; init; }
}

/// <summary>The credits of a member. Only the name, never email or birthday.</summary>
public record BookingCreditBalanceDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    /// <summary>Sum of all grants (corrections are negative).</summary>
    public required int Granted { get; init; }

    /// <summary>Credits held by purchases that are awaiting payment, processing or paid.</summary>
    public required int Used { get; init; }

    public required int Balance { get; init; }
    public required List<BookingCreditGrantDto> Grants { get; init; }
}

public record BookingCreditGrantDto
{
    public required int Id { get; init; }
    public required int Count { get; init; }
    public required BookingCreditReason Reason { get; init; }
    public string? Note { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Who granted it; <c>null</c> for the migration of the guest cards or in support mode.</summary>
    public string? GrantedByName { get; init; }
}

/// <summary>Body of <c>POST CourtSales/credits</c>: gives (positive) or takes back (negative) credits. The balance can't go below 0.</summary>
public record GrantBookingCreditsModel
{
    public int MemberId { get; init; }
    public int Count { get; init; }
    public string? Note { get; init; }
}

/// <summary>A member a treasurer can give credits to (<c>GET CourtSales/credits/members?search=</c>). Names only.</summary>
public record BookingCreditCandidateDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? ProfilePictureUrl { get; init; }
}
