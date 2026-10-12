using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Members;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary>
/// Name and picture of a player: a member of the club, or a player of another club when both clubs share their players (ATP).
/// This is all a member gets to know about somebody else, see <see cref="GetMember"/> for the full record.
/// </summary>
public record GetMemberProfile(int MemberId) : IQuery<MemberProfileResult>
{
    public class Handler(AppDbContext context, ITenantService tenantService) : IRequestHandler<GetMemberProfile, MemberProfileResult>
    {
        public async Task<MemberProfileResult> Handle(GetMemberProfile request, CancellationToken cancellationToken)
        {
            var currentClubId = tenantService.GetTenantId();

            return await (from user in context.Users
                          join member in context.Set<Member>().IgnoreQueryFilters() on user.Id equals member.UserId
                          join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id

                          // IgnoreQueryFilters switches the tenant filter off for the whole query, so the club is checked here
                          where member.Id == request.MemberId
                             && (currentClubId == null
                                 || member.ClubId == currentClubId
                                 || (club.IsAtpClub && context.Clubs.Any(current => current.Id == currentClubId && current.IsAtpClub)))

                          select new MemberProfileResult
                          {
                              MemberId = member.Id,
                              FirstName = user.FirstName,
                              LastName = user.LastName,
                              ProfilePictureUrl = context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null,
                          }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw new EntityNotFoundException(typeof(Member), request.MemberId);
        }
    }
}
