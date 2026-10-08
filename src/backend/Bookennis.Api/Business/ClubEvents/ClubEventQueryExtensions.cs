using Bookennis.Api.Data;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Members;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEvents;

public static class ClubEventQueryExtensions
{
    public static Task<ClubEvent> GetEventWithDetails(this IQueryable<ClubEvent> events, int clubId, int clubEventId, CancellationToken cancellationToken)
        => events
            .Include(e => e.Questions).ThenInclude(q => q.Options)
            .Include(e => e.Registrations).ThenInclude(r => r.Answers)
            .AsSplitQuery()
            .SingleRequiredAsync(e => e.Id == clubEventId && e.ClubId == clubId, cancellationToken);

    /// <summary>
    /// The <see cref="ClubMember"/> of the user in the club. Guest members do not take part in club events.
    /// A user can also have a guest member in the same club, so filter by member type (never use Single here).
    /// </summary>
    public static Task<int?> GetClubMemberId(this AppDbContext context, int userId, int clubId, CancellationToken cancellationToken)
        => context.Set<Member>()
            .Where(m => m.UserId == userId && m.ClubId == clubId && m.MemberType == MemberType.ClubMember)
            .OrderBy(m => m.Id)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<int> GetRequiredClubMemberId(this AppDbContext context, int userId, int clubId, CancellationToken cancellationToken)
        => await context.GetClubMemberId(userId, clubId, cancellationToken)
           ?? throw new EntityNotFoundException(typeof(ClubMember));
}
