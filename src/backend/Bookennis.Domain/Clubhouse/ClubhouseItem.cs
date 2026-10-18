using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Clubhouse;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Clubhouse.ClubhouseItemCategory</c>.</summary>
public enum ClubhouseItemCategory
{
    Drink = 0,
    Food = 1,
    Other = 2,
}

/// <summary>
/// Something the clubhouse host sells (a beer, a toast, an ice cream). The tabs keep the name and the price an item had when it was
/// booked, so changing or deleting an item never changes a tab.
/// </summary>
[Guid("5B0E7C3A-91D4-4F26-8A1B-6C2D9E4F7A31")]
public class ClubhouseItem : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNameLength = 60;

    /// <summary>1000 € - a typo guard, nothing on a clubhouse menu costs more.</summary>
    public const int MaxPriceCents = 100_000;

    public const string DefaultCurrency = "eur";

#pragma warning disable CS8618
    private ClubhouseItem() { }
#pragma warning restore CS8618

    public ClubhouseItem(int clubId, ItemData data)
    {
        ClubId = clubId;
        Currency = DefaultCurrency;
        Update(data);
    }

    public string Name { get; private set; }
    public ClubhouseItemCategory Category { get; private set; }
    public int PriceCents { get; private set; }
    public string Currency { get; private set; }

    /// <summary><c>false</c>: hidden from the host's buttons (sold out, out of season), but kept for the next time.</summary>
    public bool IsAvailable { get; private set; }

    public int SortOrder { get; private set; }

    public record ItemData(string? Name, ClubhouseItemCategory Category, int PriceCents, bool IsAvailable, int SortOrder);

    [MemberNotNull(nameof(Name))]
    public void Update(ItemData data)
    {
        var name = data.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseItemNameInvalid, [MaxNameLength.ToString()], "Invalid name.");

        if (data.PriceCents < 0 || data.PriceCents > MaxPriceCents)
            throw new PreconditionException(ClubhouseErrorCode.ClubhouseItemPriceInvalid, [MaxPriceCents.ToString()], "Invalid price.");

        if (!Enum.IsDefined(data.Category))
            throw new ArgumentOutOfRangeException(nameof(data), data.Category, "Unknown category.");

        Name = name;
        Category = data.Category;
        PriceCents = data.PriceCents;
        IsAvailable = data.IsAvailable;
        SortOrder = data.SortOrder;
    }
}
