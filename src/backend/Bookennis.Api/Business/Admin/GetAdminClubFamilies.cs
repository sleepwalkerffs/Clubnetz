using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Controller.Families;
using Bookennis.Shared.Controller.Shared;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public class GetAdminClubFamilies(int clubId, PaginationParameters pagination, string? searchTerm)
    : PagingQuery<AdminFamilyResult, GetAdminClubFamiliesResult>(pagination.Page, pagination.PageSize, pagination.EnablePaging)
{
    public int ClubId { get; } = clubId;
    public string? SearchTerm { get; } = QueryUtil.Escape(searchTerm);

    public class Handler(AppDbContext context) : IRequestHandler<GetAdminClubFamilies, GetAdminClubFamiliesResult>
    {
        public async Task<GetAdminClubFamiliesResult> Handle(GetAdminClubFamilies request, CancellationToken cancellationToken)
        {
            var query = context.Families
                .Where(f => f.ClubId == request.ClubId);

            if (request.SearchTerm is not null)
            {
                query = query.Where(f =>
                    f.Parents.Any(p => context.ClubMembers
                        .Where(m => m.Id == p.MemberId)
                        .Join(context.Users, m => m.UserId, u => u.Id, (m, u) => u)
                        .Any(u => EF.Functions.ILike(u.FullName, request.SearchTerm!))) ||
                    f.Children.Any(c => context.ClubMembers
                        .Where(m => m.Id == c.MemberId)
                        .Join(context.Users, m => m.UserId, u => u.Id, (m, u) => u)
                        .Any(u => EF.Functions.ILike(u.FullName, request.SearchTerm!))));
            }

            var result = await query
                .OrderBy(f => f.Id)
                .Select(f => new AdminFamilyResult
                {
                    FamilyId = f.Id,
                    Parents = f.Parents.Select(p => new FamilyParentDto
                    {
                        MemberId = p.MemberId,
                        FirstName = context.Users.Where(u => context.ClubMembers.Where(m => m.Id == p.MemberId).Select(m => m.UserId).Contains(u.Id)).Select(u => u.FirstName).FirstOrDefault() ?? "",
                        LastName = context.Users.Where(u => context.ClubMembers.Where(m => m.Id == p.MemberId).Select(m => m.UserId).Contains(u.Id)).Select(u => u.LastName).FirstOrDefault() ?? ""
                    }).ToList(),
                    Children = f.Children.Select(c => new FamilyChildDto
                    {
                        MemberId = c.MemberId,
                        FirstName = context.Users.Where(u => context.ClubMembers.Where(m => m.Id == c.MemberId).Select(m => m.UserId).Contains(u.Id)).Select(u => u.FirstName).FirstOrDefault() ?? "",
                        LastName = context.Users.Where(u => context.ClubMembers.Where(m => m.Id == c.MemberId).Select(m => m.UserId).Contains(u.Id)).Select(u => u.LastName).FirstOrDefault() ?? "",
                        IsOwnedAccount = context.Users.Where(u => context.ClubMembers.Where(m => m.Id == c.MemberId).Select(m => m.UserId).Contains(u.Id)).Select(u => u.BelongsToUserId).FirstOrDefault() != null,
                        Birthday = context.Users.Where(u => context.ClubMembers.Where(m => m.Id == c.MemberId).Select(m => m.UserId).Contains(u.Id)).Select(u => u.Birthday).FirstOrDefault(),
                        Gender = (Gender)context.Users.Where(u => context.ClubMembers.Where(m => m.Id == c.MemberId).Select(m => m.UserId).Contains(u.Id)).Select(u => u.Gender).FirstOrDefault()
                    }).ToList()
                })
                .AsNoTracking()
                .ToPagedResultAsync(request, cancellationToken);

            return new GetAdminClubFamiliesResult
            {
                Entities = result.Entities,
                Total = result.Total,
                Page = result.Page
            };
        }
    }
}
