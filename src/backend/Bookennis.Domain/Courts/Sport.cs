using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Courts;

/// <summary>
/// The sport a court is for. A club with courts of more than one sport sells memberships per sport:
/// an enrollment (<see cref="Members.MemberSeason.Sports"/>) says which courts a member books for free.
/// Stored as a number: never change the values.
/// </summary>
public enum Sport
{
    Tennis = 0,
    Padel = 1,
}

/// <summary>Lists of sports where an empty list means "every sport" (enrollments, membership plans).</summary>
public static class Sports
{
    public static bool Covers(IReadOnlyCollection<Sport> sports, Sport sport) => sports.Count == 0 || sports.Contains(sport);

    /// <summary>The two lists share a sport (an empty list shares every sport).</summary>
    public static bool Overlap(IReadOnlyCollection<Sport> a, IReadOnlyCollection<Sport> b) => a.Count == 0 || b.Count == 0 || a.Intersect(b).Any();

    /// <summary>Both lists together; empty (every sport) when one of them is empty.</summary>
    public static Sport[] Union(IReadOnlyCollection<Sport> a, IReadOnlyCollection<Sport> b) => a.Count == 0 || b.Count == 0 ? [] : Normalize(a.Concat(b));

    /// <summary>Distinct and ordered; unknown values are refused.</summary>
    public static Sport[] Normalize(IEnumerable<Sport>? sports)
    {
        var list = (sports ?? []).Distinct().Order().ToArray();
        foreach (var sport in list)
            EnsureKnown(sport);

        return list;
    }

    /// <summary>The sports spelled out: an empty list (every sport) becomes every known sport.</summary>
    public static Sport[] Expand(IReadOnlyCollection<Sport> sports) => sports.Count == 0 ? Enum.GetValues<Sport>() : Normalize(sports);

    /// <summary>The other way round: every known sport becomes the empty list.</summary>
    public static Sport[] Compress(IEnumerable<Sport> sports)
    {
        var list = Normalize(sports);
        return list.Length == Enum.GetValues<Sport>().Length ? [] : list;
    }

    /// <summary>A value that is not a sport (a script sending <c>5</c>) is invalid input for the client, not a server error.</summary>
    public static Sport EnsureKnown(Sport sport)
        => Enum.IsDefined(sport) ? sport : throw new PreconditionException(ErrorCode.InvalidInput, $"Unknown sport {(int)sport}.");

    public enum ErrorCode
    {
        /// <summary>Shares the client's text for invalid input.</summary>
        InvalidInput,
    }
}
