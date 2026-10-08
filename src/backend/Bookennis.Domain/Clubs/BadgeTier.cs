using Bookennis.Domain.Base;

namespace Bookennis.Domain.Clubs;

public class BadgeTier : TenantDomainEntity
{
#pragma warning disable CS8618
    private BadgeTier() { }
#pragma warning restore CS8618

    public BadgeTier(int clubId, int seasonId, int level, string name, string description, int matchesRequired, int sortOrder)
    {
        ClubId = clubId;
        SeasonId = seasonId;
        Level = level;
        Name = name;
        Description = description;
        MatchesRequired = matchesRequired;
        SortOrder = sortOrder;
    }

    public int SeasonId { get; private set; }
    public int Level { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public int MatchesRequired { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string name, string description, int matchesRequired)
    {
        Name = name;
        Description = description;
        MatchesRequired = matchesRequired;
    }
}
