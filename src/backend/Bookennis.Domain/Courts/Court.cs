using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Courts.Events;
using Bookennis.Global;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Courts;

[Guid("2DD0B4D1-6089-43A3-A1A9-10E1E69550CE")]
public class Court : TenantDomainEntity, IAggregateRoot
{
#pragma warning disable 8618
    private Court() { }
#pragma warning restore 8618

    public Court(int clubId, string name, string alias, int sortOrder = 0)
    {
        Name = name.Clean();
        ClubId = clubId;
        Alias = alias;
        SortOrder = sortOrder;
    }

    public string Name { get; private set; }
    public string Alias { get; private set; }
    public int SortOrder { get; private set; }

    public DateTimeOffsetInterval? Inactive { get; private set; }

    public void RenameCourt(string newName) => Name = newName;
    public void RenameCourtAlias(string alias) => Alias = alias;
    public void Order(int sortOrder) => SortOrder = sortOrder;
    public void ReactivateCourt() => Inactive = null;
    public void SetInactive(DateTimeOffsetInterval interval)
    {
        if (Inactive is null || Inactive.From != interval.From || Inactive?.To != interval.To)
            AddDomainEvent(new CourtSetInactiveDomainEvent());

        Inactive = interval;
    }

    public bool IsInactive(DateTimeOffsetInterval offsetInterval) => Inactive is not null && Inactive.Intersects(offsetInterval);
}