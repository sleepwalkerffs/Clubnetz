using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubProfile;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubProfile;

/// <summary>
/// <see cref="SupportClubId"/> is set for application administrators: the club they opened. If they are not a member
/// of it, they get a profile without a member, with the rights of a club admin (support mode).
/// </summary>
public record GetClubProfile(int UserId, bool IsGuestSession, int? SupportClubId = null) : IQuery<GetClubProfileResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubProfile, GetClubProfileResult>
    {
        public async Task<GetClubProfileResult> Handle(GetClubProfile request, CancellationToken cancellationToken)
        {
            var memberQuery = context.Set<Member>()
                .Where(m => m.UserId == request.UserId);

            memberQuery = request.IsGuestSession
                ? memberQuery.Where(m => m.MemberType == MemberType.GuestMember)
                : memberQuery.Where(m => m.MemberType == MemberType.ClubMember);

            var result = await (from club in context.Clubs
                                join member in memberQuery on club.Id equals member.ClubId
                                select new GetClubProfileResult
                                {
                                    ClubName = club.Name,
                                    MemberId = member.Id,
                                    Role = member.UserRoles.Select(x => (Bookennis.Shared.Controller.Shared.MemberRole)x).ToArray(),
                                }).FirstOrDefaultAsync(cancellationToken);

            // Fallback: if no member found with the specific type, try any
            result ??= await (from club in context.Clubs
                              join member in context.Set<Member>() on club.Id equals member.ClubId
                              where member.UserId == request.UserId
                              select new GetClubProfileResult
                              {
                                  ClubName = club.Name,
                                  MemberId = member.Id,
                                  Role = member.UserRoles.Select(x => (Bookennis.Shared.Controller.Shared.MemberRole)x).ToArray(),
                              }).FirstOrDefaultAsync(cancellationToken);

            if (result is null && request.SupportClubId is { } supportClubId)
            {
                result = await context.Clubs
                    .Where(club => club.Id == supportClubId)
                    .Select(club => new GetClubProfileResult
                    {
                        ClubName = club.Name,
                        MemberId = 0,
                        Role = new[] { Bookennis.Shared.Controller.Shared.MemberRole.Admin },
                        IsSupportMode = true,
                    }).FirstOrDefaultAsync(cancellationToken);
            }

            return result ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException();
        }
    }
}