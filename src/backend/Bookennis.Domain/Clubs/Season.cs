using Bookennis.Domain.Base;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Clubs;

public class Season : TenantDomainEntity
{
#pragma warning disable 8618
    private Season() { }
#pragma warning restore 8618

    public Season(int clubId, DateOnlyInterval period)
    {
        ClubId = clubId;
        Period = period;
    }

    public DateOnlyInterval Period { get; private set; }

    public void Update(DateOnlyInterval period)
    {
        Period = period;
    }

    public bool IsActiveOn(DateOnly date) => Period.From <= date && date <= Period.To;
}
