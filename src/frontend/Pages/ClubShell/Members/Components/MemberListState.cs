using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Pages.ClubShell.Members.Components;

/// <summary>
/// State of the members list (filter, sorting and paging). It is kept in the URL, so going back from a
/// member restores the list exactly as it was.
/// </summary>
public sealed record MemberListState
{
    public const int DefaultPageSize = 25;
    public static readonly int[] PageSizes = [10, 25, 50, 100];

    public string? Search { get; init; }
    public IReadOnlyList<MemberRole> Roles { get; init; } = [];
    public bool NewMembersOnly { get; init; }
    public IReadOnlyList<int> InSeasonIds { get; init; } = [];
    public IReadOnlyList<int> NotInSeasonIds { get; init; } = [];
    public int? NoBookingsInSeasonId { get; init; }
    public IReadOnlyList<Gender> Genders { get; init; } = [];
    public IReadOnlyList<AgeGroup> AgeGroups { get; init; } = [];
    public bool? HasEmail { get; init; }

    /// <summary>1-based page.</summary>
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public string SortBy { get; init; } = nameof(MemberResult.LastName);
    public bool SortDescending { get; init; }

    public int ActiveFilterCount
        => (string.IsNullOrWhiteSpace(Search) ? 0 : 1)
           + (Roles.Count > 0 ? 1 : 0)
           + (NewMembersOnly ? 1 : 0)
           + InSeasonIds.Count
           + NotInSeasonIds.Count
           + (NoBookingsInSeasonId is null ? 0 : 1)
           + (Genders.Count > 0 ? 1 : 0)
           + (AgeGroups.Count > 0 ? 1 : 0)
           + (HasEmail is null ? 0 : 1);

    public MemberFilter ToFilter() => new()
    {
        SearchTerm = string.IsNullOrWhiteSpace(Search) ? null : Search,
        Roles = Roles.Count > 0 ? Roles.ToArray() : null,
        NewMembersOnly = NewMembersOnly,
        InSeasonIds = InSeasonIds.Count > 0 ? InSeasonIds.ToArray() : null,
        NotInSeasonIds = NotInSeasonIds.Count > 0 ? NotInSeasonIds.ToArray() : null,
        NoBookingsInSeasonId = NoBookingsInSeasonId,
        Genders = Genders.Count > 0 ? Genders.ToArray() : null,
        AgeGroups = AgeGroups.Count > 0 ? AgeGroups.ToArray() : null,
        HasEmail = HasEmail,
    };

    /// <summary>A copy with the same sorting and page size but without filters, on the first page.</summary>
    public MemberListState WithoutFilters() => new() { PageSize = PageSize, SortBy = SortBy, SortDescending = SortDescending };

    /// <summary>Query parameters for the URL; default values are left out (null removes the parameter).</summary>
    public Dictionary<string, object?> ToQuery() => new()
    {
        [QueryKeys.Search] = string.IsNullOrWhiteSpace(Search) ? null : Search,
        [QueryKeys.Role] = Roles.Count > 0 ? Roles.Select(r => (int)r).ToArray() : null,
        [QueryKeys.New] = NewMembersOnly ? true : null,
        [QueryKeys.In] = InSeasonIds.Count > 0 ? InSeasonIds.ToArray() : null,
        [QueryKeys.NotIn] = NotInSeasonIds.Count > 0 ? NotInSeasonIds.ToArray() : null,
        [QueryKeys.NoBookingsIn] = NoBookingsInSeasonId,
        [QueryKeys.Gender] = Genders.Count > 0 ? Genders.Select(g => (int)g).ToArray() : null,
        [QueryKeys.Age] = AgeGroups.Count > 0 ? AgeGroups.Select(a => (int)a).ToArray() : null,
        [QueryKeys.Email] = HasEmail,
        [QueryKeys.Page] = Page > 1 ? Page : null,
        [QueryKeys.Size] = PageSize != DefaultPageSize ? PageSize : null,
        [QueryKeys.Sort] = SortBy != nameof(MemberResult.LastName) || SortDescending ? $"{SortBy}_{(SortDescending ? "desc" : "asc")}" : null,
    };

    public static MemberListState FromQuery(
        string? search, int[]? roles, bool? newMembersOnly, int[]? inSeasons, int[]? notInSeasons, int? noBookingsIn,
        int[]? genders, int[]? ageGroups, bool? hasEmail, int? page, int? pageSize, string? sort)
    {
        var sortParts = sort?.Split('_', 2);
        var sortBy = sortParts?[0] is { Length: > 0 } key && SortableColumns.Contains(key) ? key : nameof(MemberResult.LastName);

        return new MemberListState
        {
            Search = search,
            Roles = ValidEnums<MemberRole>(roles),
            NewMembersOnly = newMembersOnly == true,
            InSeasonIds = inSeasons?.Distinct().ToList() ?? [],
            NotInSeasonIds = notInSeasons?.Distinct().ToList() ?? [],
            NoBookingsInSeasonId = noBookingsIn,
            Genders = ValidEnums<Gender>(genders),
            AgeGroups = ValidEnums<AgeGroup>(ageGroups),
            HasEmail = hasEmail,
            Page = page is > 0 ? page.Value : 1,
            PageSize = pageSize is { } size && PageSizes.Contains(size) ? size : DefaultPageSize,
            SortBy = sortBy,
            SortDescending = sortParts is [_, "desc"],
        };
    }

    public static readonly string[] SortableColumns =
    [
        nameof(MemberResult.LastName),
        nameof(MemberResult.FirstName),
        nameof(MemberResult.Birthday),
        nameof(MemberResult.ContactEmail),
        nameof(MemberResult.LastPlayed),
    ];

    private static List<T> ValidEnums<T>(int[]? values) where T : struct, Enum
        => values?.Where(v => Enum.IsDefined(typeof(T), v)).Select(v => (T)(object)v).Distinct().ToList() ?? [];

    public static class QueryKeys
    {
        public const string Search = "q";
        public const string Role = "role";
        public const string New = "new";
        public const string In = "in";
        public const string NotIn = "notIn";
        public const string NoBookingsIn = "noBookingsIn";
        public const string Gender = "gender";
        public const string Age = "age";
        public const string Email = "email";
        public const string Page = "page";
        public const string Size = "size";
        public const string Sort = "sort";
    }
}
