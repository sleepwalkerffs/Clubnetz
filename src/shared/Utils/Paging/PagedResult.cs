namespace Bookennis.Shared.Utils.Paging;

public class PagedResult<T>
{
    public required int Total { get; init; }
    public required int Page { get; init; }
    public required List<T> Entities { get; init; }
}