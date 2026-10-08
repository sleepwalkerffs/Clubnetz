using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminUpdateClubMember(int ClubId, int MemberId, MemberRole[] Roles, int[] AllowedSeasonIds, int BookingsPerWeek) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminUpdateClubMember>
    {
        public async Task<Unit> Handle(AdminUpdateClubMember request, CancellationToken cancellationToken)
        {
            var member = await context.ClubMembers.SingleRequiredAsync(m => m.Id == request.MemberId && m.ClubId == request.ClubId, cancellationToken);

            member.UpdateRoles(request.Roles);
            member.Update(request.BookingsPerWeek);

            var existingSeasonLinks = await context.MemberSeasons
                .Where(ms => ms.MemberId == request.MemberId)
                .ToListAsync(cancellationToken);

            var toRemove = existingSeasonLinks.Where(ms => !request.AllowedSeasonIds.Contains(ms.SeasonId)).ToList();
            var toAdd = request.AllowedSeasonIds
                .Where(sid => existingSeasonLinks.All(ms => ms.SeasonId != sid))
                .Select(sid => new MemberSeason(request.MemberId, sid))
                .ToList();

            context.MemberSeasons.RemoveRange(toRemove);
            context.MemberSeasons.AddRange(toAdd);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
