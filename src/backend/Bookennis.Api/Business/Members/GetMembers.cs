using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Infrastructure.Utils.Sorting;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils.Sorting;
using Microsoft.EntityFrameworkCore;
using SortParameters = Bookennis.Api.Infrastructure.Utils.Sorting.SortParameters;

namespace Bookennis.Api.Business.Members;

public class GetMembers(PaginationParameters pagination, SortParameters? sort, MemberFilter? filter = null)
    : PagingQuery<MemberResult, GetMembersResult>(pagination.Page, pagination.PageSize, pagination.EnablePaging)
{
    public SortParameters? Sort { get; init; } = sort;
    public MemberFilter Filter { get; init; } = filter ?? new MemberFilter();

    public class Handler(AppDbContext context) : IRequestHandler<GetMembers, GetMembersResult>
    {
        public async Task<GetMembersResult> Handle(GetMembers request, CancellationToken cancellationToken)
        {
            // If sorting is provided always order by MemberId to avoid sorting issue if sort key is not unique
            request.Sort?.TryAdd(nameof(MemberResult.LastName), SortDirection.Ascending);
            request.Sort?.TryAdd(nameof(MemberResult.MemberId), SortDirection.Ascending);

            var now = DateTimeOffset.UtcNow;
            var members = await MemberFilterQuery.Build(context, request.Filter, cancellationToken);

            var result = await (from row in members
                                join club in context.Clubs on row.Member.ClubId equals club.Id
                                join familyMember in context.FamilyMembers on row.Member.Id equals familyMember.MemberId into tmpFamilyMember
                                from familyMember in tmpFamilyMember.DefaultIfEmpty()

                                orderby row.User.LastName, row.User.FirstName

                                select new MemberResult
                                {
                                    MemberId = row.Member.Id,
                                    FamilyId = familyMember.FamilyId,
                                    Email = row.User.Email,
                                    ContactEmail = row.ContactEmail,
                                    FullName = row.User.FullName,
                                    FirstName = row.User.FirstName,
                                    LastName = row.User.LastName,
                                    Gender = (Gender)row.User.Gender,
                                    Birthday = row.User.Birthday,
                                    Roles = row.Member.UserRoles.Select(x => (MemberRole)x).ToArray(),
                                    BookingsPerWeek = row.Member.BookingsPerWeek == null ? club.ConcurrentAllowedBookings : row.Member.BookingsPerWeek!.Value,
                                    AllowedSeasonIds = context.MemberSeasons
                                        .Where(ms => ms.MemberId == row.Member.Id)
                                        .Select(ms => ms.SeasonId)
                                        .ToArray(),
                                    ProfilePictureUrl = context.UserProfilePictures.Any(p => p.UserId == row.User.Id) ? "/api/Profile/picture/" + row.User.Id : null,
                                    LastPlayed = context.BookingPlayers
                                        .Where(bp => bp.MemberId == row.Member.Id && bp.Booking.Interval.From <= now)
                                        .Max(bp => (DateTimeOffset?)bp.Booking.Interval.From),
                                }).Sort(request.Sort)
                                  .ToPagedResultAsync(request, cancellationToken);

            return new GetMembersResult
            {
                Total = result.Total,
                Page = result.Page,
                Entities = result.Entities
            };
        }
    }
}
