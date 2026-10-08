using Bookennis.Shared.Utils.Paging;

namespace Bookennis.Client.Utils.Pagination;

public static class PaginationParametersExtension
{
    public static Dictionary<string, string?> ToQueryParameters(this PaginationParameters paginationParameters)
    {
        var dict = new Dictionary<string, string?>();
        dict.Add(nameof(PaginationParameters.Page), $"{paginationParameters.Page}");
        dict.Add(nameof(PaginationParameters.PageSize), $"{paginationParameters.PageSize}");
        dict.Add(nameof(PaginationParameters.EnablePaging), $"{paginationParameters.EnablePaging}");
        return dict;
    }
}