using Bookennis.Shared.Controller.Members;

namespace Bookennis.Client.Utils.Members;

public static class MemberFilterExtensions
{
    /// <summary>Query parameters as bound by the members endpoints (arrays as repeated keys).</summary>
    public static IEnumerable<KeyValuePair<string, string?>> ToQueryPairs(this MemberFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            yield return new(nameof(MemberFilter.SearchTerm), filter.SearchTerm.Trim());

        if (filter.NewMembersOnly)
            yield return new(nameof(MemberFilter.NewMembersOnly), "true");

        if (filter.NoBookingsInSeasonId is { } seasonId)
            yield return new(nameof(MemberFilter.NoBookingsInSeasonId), seasonId.ToString());

        if (filter.HasEmail is { } hasEmail)
            yield return new(nameof(MemberFilter.HasEmail), hasEmail ? "true" : "false");

        foreach (var pair in Repeat(nameof(MemberFilter.Roles), filter.Roles?.Select(r => (int)r))
                     .Concat(Repeat(nameof(MemberFilter.InSeasonIds), filter.InSeasonIds))
                     .Concat(Repeat(nameof(MemberFilter.NotInSeasonIds), filter.NotInSeasonIds))
                     .Concat(Repeat(nameof(MemberFilter.Genders), filter.Genders?.Select(g => (int)g)))
                     .Concat(Repeat(nameof(MemberFilter.AgeGroups), filter.AgeGroups?.Select(a => (int)a))))
            yield return pair;
    }

    private static IEnumerable<KeyValuePair<string, string?>> Repeat(string key, IEnumerable<int>? values)
        => values?.Select(v => new KeyValuePair<string, string?>(key, v.ToString())) ?? [];
}
