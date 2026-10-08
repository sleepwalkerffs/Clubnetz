using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Controller.Shared;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public class GetAdminClubMembers(int clubId, PaginationParameters pagination, string? searchTerm)
    : PagingQuery<AdminClubMemberResult, GetAdminClubMembersResult>(pagination.Page, pagination.PageSize, pagination.EnablePaging)
{
    public int ClubId { get; } = clubId;
    public string? SearchTerm { get; } = QueryUtil.Escape(searchTerm);

    public class Handler(AppDbContext context) : IRequestHandler<GetAdminClubMembers, GetAdminClubMembersResult>
    {
        public async Task<GetAdminClubMembersResult> Handle(GetAdminClubMembers request, CancellationToken cancellationToken)
        {
            var result = await (from member in context.ClubMembers
                                join user in context.Users on member.UserId equals user.Id
                                where member.ClubId == request.ClubId
                                where request.SearchTerm == null
                                    || EF.Functions.ILike(user.FullName, request.SearchTerm)
                                    || EF.Functions.ILike(user.Email!, request.SearchTerm)
                                orderby user.LastName, user.FirstName
                                select new AdminClubMemberResult
                                {
                                    MemberId = member.Id,
                                    UserId = user.Id,
                                    FullName = user.FullName,
                                    Email = user.Email,
                                    Roles = member.UserRoles.Select(r => (MemberRole)r).ToArray(),
                                    AllowedSeasonIds = context.MemberSeasons
                                        .Where(ms => ms.MemberId == member.Id)
                                        .Select(ms => ms.SeasonId)
                                        .ToArray(),
                                    BookingsPerWeek = member.BookingsPerWeek,
                                })
                .AsNoTracking()
                .ToPagedResultAsync(request, cancellationToken);

            return new GetAdminClubMembersResult
            {
                Entities = result.Entities,
                Total = result.Total,
                Page = result.Page
            };
        }
    }
}
