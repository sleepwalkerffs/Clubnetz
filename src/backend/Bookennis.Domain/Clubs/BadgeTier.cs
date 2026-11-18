using Bookennis.Domain.Base;
using Bookennis.Domain.Courts;

namespace Bookennis.Domain.Clubs;

public class BadgeTier : TenantDomainEntity
{
#pragma warning disable CS8618
    private BadgeTier() { }
#pragma warning restore CS8618

    public BadgeTier(int clubId, int seasonId, int level, string name, string description, int matchesRequired, int sortOrder, Sport? sport = null)
    {
        Sport = sport is { } s ? Sports.EnsureKnown(s) : null;
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

    /// <summary>
    /// The track of the tier: only matches on courts of this sport count. <c>null</c>: matches of every sport (clubs with one sport and every tier
    /// made before sports existed). Levels are numbered per track, so a season can have a level 1 for every sport.
    /// </summary>
    public Sport? Sport { get; private set; }

    public void Update(string name, string description, int matchesRequired)
    {
        Name = name;
        Description = description;
        MatchesRequired = matchesRequired;
    }
}
