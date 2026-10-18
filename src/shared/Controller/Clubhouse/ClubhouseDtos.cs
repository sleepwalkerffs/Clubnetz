namespace Bookennis.Shared.Controller.Clubhouse;

/// <summary>Keep in sync with <c>Bookennis.Domain.Clubhouse.ClubhouseItemCategory</c>.</summary>
public enum ClubhouseItemCategory
{
    Drink = 0,
    Food = 1,
    Other = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Clubhouse.ClubhouseTabStatus</c>.</summary>
public enum ClubhouseTabStatus
{
    Open = 0,
    Paid = 1,

    /// <summary>The member is paying online: the host can't change the tab until the payment ends (at most 30 minutes).</summary>
    AwaitingPayment = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Clubhouse.ClubhousePaymentMethod</c>.</summary>
public enum ClubhousePaymentMethod
{
    Cash = 0,

    /// <summary>On the host's own card terminal; Clubnetz only records it.</summary>
    Card = 1,
    Other = 2,

    /// <summary>Paid by the member in the app, to the host's SumUp account. Only SumUp's answer books it, never the host.</summary>
    Online = 3,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Clubhouse.ClubhouseTabPaymentStatus</c>.</summary>
public enum ClubhouseTabPaymentStatus
{
    Pending = 0,
    Paid = 1,
    Expired = 2,
}

/// <summary>Something on the menu of the clubhouse.</summary>
public record ClubhouseItemDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required ClubhouseItemCategory Category { get; init; }
    public required int PriceCents { get; init; }
    public required string Currency { get; init; }

    /// <summary><c>false</c>: hidden from the buttons of the host (sold out, out of season).</summary>
    public required bool IsAvailable { get; init; }

    public required int SortOrder { get; init; }
}

/// <summary>Body of <c>POST|PUT Clubhouse/items</c>.</summary>
public record SaveClubhouseItemModel
{
    public string? Name { get; init; }
    public ClubhouseItemCategory Category { get; init; }
    public int PriceCents { get; init; }
    public bool IsAvailable { get; init; } = true;
    public int SortOrder { get; init; }
}

/// <summary>A member of the club the host can open a tab for. Name and picture only.</summary>
public record ClubhouseGuestDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? ProfilePictureUrl { get; init; }
}

/// <summary>An open tab on the host's start screen.</summary>
public record ClubhouseOpenTabDto
{
    public required int Id { get; init; }

    /// <summary><c>null</c> for a guest from outside, or once the member left the club.</summary>
    public int? MemberId { get; init; }

    /// <summary>The name of the member or of the guest from outside; empty once the member left the club.</summary>
    public required string Name { get; init; }

    public required bool IsWalkIn { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public required int TotalCents { get; init; }
    public required int ItemCount { get; init; }

    /// <summary>The member is paying online right now: the tab is locked.</summary>
    public bool IsAwaitingPayment { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }

    /// <summary>When the host booked onto it last; the most recent tabs come first.</summary>
    public required DateTimeOffset LastActivityAt { get; init; }
}

/// <summary>What the host sold today (club time zone): paid tabs by payment method and what is still open.</summary>
public record ClubhouseDaySummaryDto
{
    public required int PaidTabCount { get; init; }
    public required int CashCents { get; init; }
    public required int CardCents { get; init; }
    public required int OtherCents { get; init; }
    public int OnlineCents { get; init; }
    public required int OpenCents { get; init; }
}

/// <summary>
/// The start screen of the host (<c>GET Clubhouse/board</c>): the open tabs, then the members who play today (closest to now first),
/// then the regulars (most tabs in the last weeks). Members who already have an open tab are only listed there.
/// </summary>
public record ClubhouseBoardResult
{
    public required List<ClubhouseOpenTabDto> OpenTabs { get; init; }
    public required List<ClubhouseGuestDto> PlayingToday { get; init; }
    public required List<ClubhouseGuestDto> Regulars { get; init; }
    public required ClubhouseDaySummaryDto Today { get; init; }
}

/// <summary>Body of <c>POST Clubhouse/tabs</c>: a tab for a member of the club or for a guest from outside (a name).</summary>
public record OpenClubhouseTabModel
{
    public int? MemberId { get; init; }
    public string? GuestName { get; init; }
}

public record ClubhouseTabLineDto
{
    public required int Id { get; init; }
    public int? ItemId { get; init; }
    public required string Name { get; init; }
    public required ClubhouseItemCategory Category { get; init; }
    public required int UnitPriceCents { get; init; }
    public required int Quantity { get; init; }
    public required int TotalCents { get; init; }
    public required DateTimeOffset AddedAt { get; init; }
}

public record ClubhouseTabDto
{
    public required int Id { get; init; }
    public int? MemberId { get; init; }

    /// <summary>The name of the member or of the guest from outside; empty once the member left the club.</summary>
    public required string Name { get; init; }

    public required bool IsWalkIn { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public required ClubhouseTabStatus Status { get; init; }
    public required string Currency { get; init; }
    public required int TotalCents { get; init; }
    public required DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public ClubhousePaymentMethod? PaymentMethod { get; init; }

    /// <summary>Newest first.</summary>
    public required List<ClubhouseTabLineDto> Lines { get; init; }
}

public record ClubhouseTabItemModel
{
    public int ItemId { get; init; }
    public int Quantity { get; init; } = 1;
}

/// <summary>Body of <c>POST Clubhouse/tabs/{tabId}/items</c>: everything the host tapped for one round, booked at once.</summary>
public record AddClubhouseTabItemsModel
{
    public List<ClubhouseTabItemModel> Items { get; init; } = [];
}

/// <summary>Body of <c>POST Clubhouse/tabs/{tabId}/pay</c>.</summary>
public record PayClubhouseTabModel
{
    public ClubhousePaymentMethod PaymentMethod { get; init; }
}

/// <summary>The paid tabs of a period for the host's accounts (<c>GET Clubhouse/tabs?from&amp;to</c>, newest first).</summary>
public record GetClubhouseTabsResult
{
    public required List<ClubhouseTabDto> Tabs { get; init; }
    public required int CashCents { get; init; }
    public required int CardCents { get; init; }
    public required int OtherCents { get; init; }
    public int OnlineCents { get; init; }
}

/// <summary>The tabs of the signed-in member and of the children of their families (<c>GET Clubhouse/my-tabs</c>).</summary>
public record MyClubhouseTabsResult
{
    public required List<ClubhouseTabDto> Open { get; init; }

    /// <summary>The last paid tabs, newest first.</summary>
    public required List<ClubhouseTabDto> RecentlyPaid { get; init; }

    /// <summary>The open tabs can be paid in the app: the host connected a SumUp account.</summary>
    public bool CanPayOnline { get; init; }
}

/// <summary>A page of SumUp to send the browser to with a full page load (<c>POST Clubhouse/sumup/connect</c>, <c>POST Clubhouse/tabs/{tabId}/online-payment</c>).</summary>
public record ClubhouseRedirectResult
{
    public required string Url { get; init; }
}

/// <summary>
/// The SumUp account of the host (<c>GET Clubhouse/sumup</c>). Status only: the tokens never leave the server.
/// </summary>
public record ClubhouseSumUpAccountDto
{
    /// <summary>The installation has an OAuth app at SumUp; without it the clubhouse works as before, without paying in the app.</summary>
    public required bool IsConfigured { get; init; }

    public required bool IsConnected { get; init; }

    /// <summary>SumUp refused the account (revoked or expired): members can't pay in the app until the host connects it again.</summary>
    public bool NeedsReconnect { get; init; }

    public string? MerchantCode { get; init; }
    public string? MerchantName { get; init; }
    public DateTimeOffset? ConnectedAt { get; init; }

    /// <summary>Payment pages that are open right now: the account can't be disconnected while there are any.</summary>
    public int PendingPayments { get; init; }
}

/// <summary>An online payment of a tab (<c>GET Clubhouse/tabs/{tabId}/online-payment/{paymentId}</c>), checked with SumUp first.</summary>
public record ClubhouseTabPaymentDto
{
    public required int Id { get; init; }
    public required int TabId { get; init; }
    public required ClubhouseTabPaymentStatus Status { get; init; }
    public required int AmountCents { get; init; }
    public required string Currency { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
}
