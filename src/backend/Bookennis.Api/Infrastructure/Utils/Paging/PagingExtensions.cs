using Bookennis.Shared.Utils.Paging;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Utils.Paging;

public static class PagingExtensions
{
    public static Task<PagedResult<T>> ToPagedResultAsync<T, TResult>(this IQueryable<T> query, PagingQuery<T, TResult> request, CancellationToken cancellationToken = default)
        where TResult : PagedResult<T>
        => query.ToPagedResultAsync(request.Page, request.PageSize, PagingDefaults.MaxPageSize, request.EnablePaging, null, cancellationToken);

    public static Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PagingQuery<T> request, CancellationToken cancellationToken = default)
        => query.ToPagedResultAsync(request.Page, request.PageSize, PagingDefaults.MaxPageSize, request.EnablePaging, null, cancellationToken);

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        int maxPageSize = PagingDefaults.MaxPageSize,
        bool enablePaging = true,
        int? overrideCount = null,
        CancellationToken cancellationToken = default)
    {
        if (enablePaging)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

            if (pageSize < 1 || pageSize > maxPageSize)
                throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var totalEntities = overrideCount ?? await query.CountAsync(cancellationToken);
        if (totalEntities == 0)
            return new PagedResult<T>
            {
                Entities = [],
                Page = 1,
                Total = 0
            };

        if (enablePaging)
        {
            // when the page is higher than expected we still want to get the last page
            var offset = page - 1;
            if (offset * pageSize >= totalEntities)
            {
                page = (int)Math.Ceiling((double)totalEntities / pageSize);
                offset = page - 1;
            }

            query = query.Skip(offset * pageSize)
                         .Take(pageSize);
        }
        else
        {
            page = 1;
        }

        var entities = await query.ToListAsync(cancellationToken);
        return new PagedResult<T>
        {
            Entities = entities,
            Page = page,
            Total = totalEntities
        };
    }
}