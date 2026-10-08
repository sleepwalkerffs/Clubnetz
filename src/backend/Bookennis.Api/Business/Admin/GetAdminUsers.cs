using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Infrastructure.Utils.Sorting;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils.Sorting;
using Microsoft.EntityFrameworkCore;
using SortParameters = Bookennis.Api.Infrastructure.Utils.Sorting.SortParameters;

namespace Bookennis.Api.Business.Admin;

public class GetAdminUsers(PaginationParameters pagination, SortParameters? sort, string? searchTerm)
    : PagingQuery<AdminUserResult, GetAdminUsersResult>(pagination.Page, pagination.PageSize, pagination.EnablePaging)
{
    public SortParameters? Sort { get; init; } = sort;
    public string? SearchTerm { get; init; } = QueryUtil.Escape(searchTerm);

    public class Handler(AppDbContext context) : IRequestHandler<GetAdminUsers, GetAdminUsersResult>
    {
        public async Task<GetAdminUsersResult> Handle(GetAdminUsers request, CancellationToken cancellationToken)
        {
            request.Sort?.TryAdd(nameof(AdminUserResult.LastName), SortDirection.Ascending);
            request.Sort?.TryAdd(nameof(AdminUserResult.Id), SortDirection.Ascending);

            var result = await context.Users
                .Where(u => u.BelongsToUserId == null)
                .Where(u => request.SearchTerm == null
                    || EF.Functions.ILike(u.FullName, request.SearchTerm)
                    || EF.Functions.ILike(u.Email!, request.SearchTerm))
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Select(u => new AdminUserResult
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    FullName = u.FullName,
                    Email = u.Email,
                    EmailConfirmed = u.EmailConfirmed,
                    Birthday = u.Birthday,
                    Gender = (Gender)u.Gender
                })
                .Sort(request.Sort)
                .ToPagedResultAsync(request, cancellationToken);

            return new GetAdminUsersResult
            {
                Entities = result.Entities,
                Total = result.Total,
                Page = result.Page
            };
        }
    }
}
