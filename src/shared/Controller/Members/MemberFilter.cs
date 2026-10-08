using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Members;

/// <summary>Filter of the club members list. The same filter is used to copy the emails of the listed members.</summary>
public class MemberFilter
{
    public string? SearchTerm { get; set; }
    public MemberRole[]? Roles { get; set; }

    /// <summary>Members of the active season that were not enrolled in the previous season.</summary>
    public bool NewMembersOnly { get; set; }

    /// <summary>Members enrolled in all of these seasons.</summary>
    public int[]? InSeasonIds { get; set; }

    /// <summary>Members enrolled in none of these seasons.</summary>
    public int[]? NotInSeasonIds { get; set; }

    /// <summary>Members without a played booking in this season.</summary>
    public int? NoBookingsInSeasonId { get; set; }

    public Gender[]? Genders { get; set; }
    public AgeGroup[]? AgeGroups { get; set; }

    /// <summary>Filters members that can (or cannot) be reached by email, either their own or a parent's.</summary>
    public bool? HasEmail { get; set; }
}

/// <summary>Age groups as used in the club statistics.</summary>
public enum AgeGroup
{
    /// <summary>12 and younger.</summary>
    Kids,

    /// <summary>13 to 17.</summary>
    Teenagers,

    /// <summary>18 to 34.</summary>
    Adults,

    /// <summary>35 and older.</summary>
    Seniors,
}
