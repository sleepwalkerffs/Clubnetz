using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubProfile;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubProfile;

/// <summary>
/// <see cref="SupportClubId"/> is set for application administrators: the club they opened. If they are not a member
/// of it, they get a profile without a member, with the rights of a club admin (support mode).
/// </summary>
public record GetClubProfile(int UserId, int? SupportClubId = null) : IQuery<GetClubProfileResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubProfile, GetClubProfileResult>
    {
        public async Task<GetClubProfileResult> Handle(GetClubProfile request, CancellationToken cancellationToken)
        {
            // Only the ClubMember: a former guest may still have a GuestMember of historic bookings, which gives no access
            var result = await (from club in context.Clubs
                                join member in context.ClubMembers on club.Id equals member.ClubId
                                where member.UserId == request.UserId && member.MemberType == MemberType.ClubMember
                                orderby member.Id
                                select new GetClubProfileResult
                                {
                                    ClubName = club.Name,
                                    MemberId = member.Id,
                                    Role = member.UserRoles.Select(x => (Bookennis.Shared.Controller.Shared.MemberRole)x).ToArray(),
                                }).FirstOrDefaultAsync(cancellationToken);

            if (result is not null)
            {
                var clubId = await context.ClubMembers.Where(m => m.Id == result.MemberId).Select(m => m.ClubId).SingleAsync(cancellationToken);
                result.IsActiveMember = await ActiveMembership.IsActive(context, clubId, result.MemberId, cancellationToken)
                                        || request.SupportClubId is not null;
                return result;
            }

            if (request.SupportClubId is { } supportClubId)
            {
                result = await context.Clubs
                    .Where(club => club.Id == supportClubId)
                    .Select(club => new GetClubProfileResult
                    {
                        ClubName = club.Name,
                        MemberId = 0,
                        Role = new[] { Bookennis.Shared.Controller.Shared.MemberRole.Admin },
                        IsSupportMode = true,
                        IsActiveMember = true,
                    }).FirstOrDefaultAsync(cancellationToken);
            }

            return result ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException();
        }
    }
}
