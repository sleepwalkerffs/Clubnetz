using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubProfile;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubProfile;

public record GetClubProfile(int UserId, bool IsGuestSession) : IQuery<GetClubProfileResult>
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

            return result ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException();
        }
    }
}