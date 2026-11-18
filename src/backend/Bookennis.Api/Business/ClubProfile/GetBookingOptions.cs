using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubProfile;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubProfile;

/// <summary>
/// Whether the user books for free like a member of the season: enrolled in the active season, or a parent who books for enrolled children.
/// Members who are not active buy court time instead (<c>CourtSales/options</c>).
/// </summary>
public record GetBookingOptions(int UserId, int ClubId) : IQuery<GetBookingOptionsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBookingOptions, GetBookingOptionsResult>
    {
        public async Task<GetBookingOptionsResult> Handle(GetBookingOptions request, CancellationToken cancellationToken)
        {
            var member = await context.ClubMembers
                .Where(m => m.UserId == request.UserId && m.ClubId == request.ClubId && m.MemberType == MemberType.ClubMember)
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (member is null)
                return new GetBookingOptionsResult { IsAllowedToBook = false };

            var isActiveMember = await ActiveMembership.IsActive(context, request.ClubId, member.Id, cancellationToken);
            var activeSeason = await context.GetActiveSeason(request.ClubId, cancellationToken);

            var ownSports = activeSeason is null
                ? null
                : await context.MemberSeasons
                    .Where(ms => ms.MemberId == member.Id && ms.SeasonId == activeSeason.Id)
                    .Select(ms => ms.Sports)
                    .FirstOrDefaultAsync(cancellationToken);
            var isInActiveSeason = ownSports is not null;

            if (!isInActiveSeason && activeSeason != null)
            {
                // Check if any of the member's children are in the active season
                var childMemberIds = await context.Families
                    .Where(f => f.ClubId == request.ClubId && f.Parents.Any(p => p.MemberId == member.Id))
                    .SelectMany(f => f.Children.Select(c => c.MemberId))
                    .ToListAsync(cancellationToken);

                if (childMemberIds.Count > 0)
                {
                    var eligibleChildren = await context.MemberSeasons
                        .Where(ms => childMemberIds.Contains(ms.MemberId) && ms.SeasonId == activeSeason.Id)
                        .Select(ms => new { ms.MemberId, ms.Sports })
                        .ToListAsync(cancellationToken);
                    var eligibleChildIds = eligibleChildren.ConvertAll(c => c.MemberId);

                    if (eligibleChildIds.Count > 0)
                    {
                        return new GetBookingOptionsResult
                        {
                            IsAllowedToBook = true,
                            IsBookingOnlyForChildren = true,
                            EligibleChildMemberIds = eligibleChildIds,
                            IsActiveMember = isActiveMember,
                            EnrolledSports = ToDto(eligibleChildren.Aggregate(eligibleChildren[0].Sports, (all, c) => Sports.Union(all, c.Sports))),
                        };
                    }
                }
            }

            // A parent books for the children too: a tennis parent can book a padel court for a padel child
            if (ownSports is { Length: > 0 } && activeSeason is not null)
            {
                var childSports = await context.Families
                    .Where(f => f.ClubId == request.ClubId && f.Parents.Any(p => p.MemberId == member.Id))
                    .SelectMany(f => f.Children.Select(c => c.MemberId))
                    .Join(context.MemberSeasons.Where(ms => ms.SeasonId == activeSeason.Id), id => id, ms => ms.MemberId, (_, ms) => ms.Sports)
                    .ToListAsync(cancellationToken);
                ownSports = childSports.Aggregate(ownSports, Sports.Union);
            }

            return new GetBookingOptionsResult
            {
                IsAllowedToBook = isInActiveSeason,
                IsActiveMember = isActiveMember,
                EnrolledSports = ToDto(ownSports ?? []),
            };
        }

        private static List<Bookennis.Shared.Controller.Shared.Sport> ToDto(Sport[] sports) => sports.Select(s => (Bookennis.Shared.Controller.Shared.Sport)s).ToList();
    }
}
