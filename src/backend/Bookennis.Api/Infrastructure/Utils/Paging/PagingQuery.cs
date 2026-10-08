using Bookennis.Shared.Utils.Paging;

namespace Bookennis.Api.Infrastructure.Utils.Paging;

public abstract class PagingQuery<T, TResult> : IQuery<TResult>, IPagingQuery
    where TResult : PagedResult<T>
{
    protected PagingQuery(int page, int pageSize, bool enablePaging = true, int maxPageSize = PagingDefaults.MaxPageSize)
    {
        Page = page;
        PageSize = pageSize;
        MaxPageSize = maxPageSize;
        EnablePaging = enablePaging;

        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

        if (pageSize < 1 || pageSize > maxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    public int Page { get; }
    public int PageSize { get; }
    public int MaxPageSize { get; }
    public bool EnablePaging { get; }
}

public abstract class PagingQuery<T>(int page, int pageSize, bool enablePaging = true, int maxPageSize = PagingDefaults.MaxPageSize)
    : PagingQuery<T, PagedResult<T>>(page, pageSize, enablePaging, maxPageSize);