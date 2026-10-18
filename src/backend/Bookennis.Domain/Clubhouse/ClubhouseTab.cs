using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Clubhouse;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Clubhouse.ClubhouseTabStatus</c>.</summary>
public enum ClubhouseTabStatus
{
    /// <summary>The host still books onto it; the guest has not paid.</summary>
    Open = 0,
    Paid = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Clubhouse.ClubhousePaymentMethod</c>.</summary>
public enum ClubhousePaymentMethod
{
    Cash = 0,

    /// <summary>On the host's own card terminal. Clubnetz only records it.</summary>
    Card = 1,
    Other = 2,
}

/// <summary>
/// The list of the clubhouse host for one guest: what a member of the club or a guest from outside (only a name) ate and drank,
/// paid at the end. A member has at most one open tab per club, so the host always books onto the same one. The money goes to
/// the host, not to the club: Clubnetz only keeps the list and records how it was paid.
/// </summary>
[Guid("E3A94D17-2C6B-4B85-9F0E-7A1C5D8B2F64")]
public class ClubhouseTab : TenantDomainEntity, IAggregateRoot
{
    public const int MaxGuestNameLength = 80;
    public const int MaxQuantity = 50;
    public const int MaxLines = 300;

    private readonly List<ClubhouseTabLine> lines = new();

#pragma warning disable CS8618
    private ClubhouseTab() { }
#pragma warning restore CS8618

    /// <summary>A tab for a member of the club (<paramref name="memberId"/>) or for a guest from outside (<paramref name="guestName"/>).</summary>
    public ClubhouseTab(int clubId, int? memberId, string? guestName, int? openedByMemberId, DateTimeOffset now)
    {
        var name = string.IsNullOrWhiteSpace(guestName) ? null : guestName.Trim();
        if ((memberId is null) == (name is null) || name?.Length > MaxGuestNameLength)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseTabGuestInvalid, [MaxGuestNameLength.ToString()], "A tab is for a member or for a guest with a name.");

        ClubId = clubId;
        MemberId = memberId;
        GuestName = name;
        OpenedByMemberId = openedByMemberId;
        OpenedAt = now;
        Currency = ClubhouseItem.DefaultCurrency;
        Status = ClubhouseTabStatus.Open;
    }

    /// <summary>The member of the club; <c>null</c> for a guest from outside and once the member left the club.</summary>
    public int? MemberId { get; private set; }

    /// <summary>The name the host gave a guest from outside ("Table 3", "Mr Huber").</summary>
    public string? GuestName { get; private set; }

    public ClubhouseTabStatus Status { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }

    /// <summary><c>null</c> in support mode and once the host left the club.</summary>
    public int? OpenedByMemberId { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }
    public ClubhousePaymentMethod? PaymentMethod { get; private set; }
    public int? PaidToMemberId { get; private set; }

    public IReadOnlyList<ClubhouseTabLine> Lines => lines.AsReadOnly();

    public bool IsOpen => Status == ClubhouseTabStatus.Open;
    public int TotalCents => lines.Sum(l => l.TotalCents);

    /// <summary>Books <paramref name="quantity"/> of an item onto the tab at its current name and price.</summary>
    public ClubhouseTabLine Add(ClubhouseItem item, int quantity, int? addedByMemberId, DateTimeOffset now)
    {
        EnsureOpen();

        if (item.ClubId != ClubId)
            throw new InvalidOperationException("The item belongs to another club.");

        if (!item.IsAvailable)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseItemNotAvailable, "The item is not on the menu.");

        if (quantity < 1 || quantity > MaxQuantity)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseQuantityInvalid, [MaxQuantity.ToString()], "Invalid quantity.");

        if (lines.Count >= MaxLines)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseTabTooManyLines, [MaxLines.ToString()], "The tab is full.");

        var line = new ClubhouseTabLine(this, item, quantity, addedByMemberId, now);
        lines.Add(line);
        return line;
    }

    /// <summary>Takes an entry off the tab (booked by mistake). <c>false</c> when there is no such entry.</summary>
    public bool Remove(int lineId)
    {
        EnsureOpen();
        return lines.RemoveAll(l => l.Id == lineId) > 0;
    }

    /// <summary>The guest paid the host: the tab is closed and a new one is opened next time.</summary>
    public void MarkPaid(ClubhousePaymentMethod method, int? paidToMemberId, DateTimeOffset now)
    {
        EnsureOpen();

        if (lines.Count == 0)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseTabEmpty, "An empty tab can't be paid.");

        Status = ClubhouseTabStatus.Paid;
        PaymentMethod = method;
        PaidToMemberId = paidToMemberId;
        PaidAt = now;
    }

    /// <summary>Throws unless the tab can be discarded: it is open and nothing is on it.</summary>
    public void EnsureCanBeDiscarded()
    {
        EnsureOpen();

        if (lines.Count > 0)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseTabNotEmpty, "Only an empty tab can be discarded.");
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseTabNotOpen, "The tab was already paid.");
    }
}

/// <summary>One booking of the host on a tab: the item with the name and price it had at that moment.</summary>
public class ClubhouseTabLine : DomainEntity
{
#pragma warning disable CS8618
    private ClubhouseTabLine() { }
#pragma warning restore CS8618

    internal ClubhouseTabLine(ClubhouseTab tab, ClubhouseItem item, int quantity, int? addedByMemberId, DateTimeOffset now)
    {
        Tab = tab;
        Item = item;
        ItemId = item.Id;
        Name = item.Name;
        Category = item.Category;
        UnitPriceCents = item.PriceCents;
        Quantity = quantity;
        AddedByMemberId = addedByMemberId;
        AddedAt = now;
    }

    public int ClubhouseTabId { get; private set; }
    public ClubhouseTab Tab { get; private set; }

    /// <summary><c>null</c> once the item was deleted; <see cref="Name"/> and <see cref="UnitPriceCents"/> stay.</summary>
    public int? ItemId { get; private set; }

    /// <summary>Only set while booking, so a new item gets its id when both are saved together.</summary>
    public ClubhouseItem? Item { get; private set; }

    public string Name { get; private set; }
    public ClubhouseItemCategory Category { get; private set; }
    public int UnitPriceCents { get; private set; }
    public int Quantity { get; private set; }
    public int? AddedByMemberId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    public int TotalCents => UnitPriceCents * Quantity;
}
