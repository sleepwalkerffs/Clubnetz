using Bookennis.Domain.Base;

namespace Bookennis.Domain.Clubs;

public class OneTimeBadge : TenantDomainEntity
{
#pragma warning disable CS8618
    private OneTimeBadge() { }
#pragma warning restore CS8618

    public OneTimeBadge(int clubId, int seasonId, string name, string description)
    {
        ClubId = clubId;
        SeasonId = seasonId;
        Name = name;
        Description = description;
    }

    public int SeasonId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }

    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
