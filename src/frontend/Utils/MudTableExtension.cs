using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;
using MudBlazor;
using SortDirection = MudBlazor.SortDirection;

namespace Bookennis.Client.Utils;

public static class MudTableExtension
{
    public static PaginationParameters ToPagingParameters(this TableState tableState) =>
        new()
        {
            Page = tableState.Page + 1,
            PageSize = tableState.PageSize,
            EnablePaging = true,
        };

    public static SortParameters? ToSortParameters(this TableState tableState) =>
        tableState.SortDirection == SortDirection.None
            ? null
            : new SortParameters
            {
                {
                    tableState.SortLabel ?? "",
                    tableState.SortDirection == SortDirection.Ascending ? Bookennis.Shared.Utils.Sorting.SortDirection.Ascending : Bookennis.Shared.Utils.Sorting.SortDirection.Descending
                },
            };
}
