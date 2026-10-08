using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

/// <summary>
/// The counterpart of <see cref="BecomeClubMember"/>: the current user ends their club membership. The membership and everything attached
/// to it (bookings history, badges, event registrations, family links) is deleted, see <see cref="MemberRemoval"/>. A guest card of the
/// same user (<c>GuestMember</c>) is not affected, and neither are the memberships of the user's children.
/// </summary>
public record LeaveClub([Required] int ClubId) : ICommand
{
    public enum ErrorCode
    {
        LastClubAdmin
    }

    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<LeaveClub>
    {
        public async Task<Unit> Handle(LeaveClub request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var userId))
                throw new AuthorizationFailedException();

            // Dual membership: only the ClubMember record, never a GuestMember of the same user
            var member = await context.ClubMembers
                .IgnoreQueryFilters()
                .Where(m => m.UserId == userId && m.ClubId == request.ClubId)
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (member is null)
                return default;

            if (await MemberRemoval.FindClubLosingItsLastAdmin(context, [member.Id], cancellationToken) is { } clubName)
                throw new PreconditionException(ErrorCode.LastClubAdmin, [clubName], $"The user is the last admin of the club {clubName}.");

            await MemberRemoval.PrepareRemoval(context, [member.Id], cancellationToken);
            context.ClubMembers.Remove(member);
            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
