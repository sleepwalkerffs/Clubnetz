using Bookennis.Api.Data;
using Bookennis.Shared.Controller.OneTimeBadges;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetOneTimeBadges(int ClubId, int SeasonId) : IQuery<GetOneTimeBadgesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetOneTimeBadges, GetOneTimeBadgesResult>
    {
        public async Task<GetOneTimeBadgesResult> Handle(GetOneTimeBadges request, CancellationToken cancellationToken)
        {
            var badges = await context.OneTimeBadges
                .Where(b => b.ClubId == request.ClubId && b.SeasonId == request.SeasonId)
                .OrderBy(b => b.Name)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Description,
                    ImageUrl = context.OneTimeBadgeImages.Any(i => i.OneTimeBadgeId == b.Id)
                        ? $"/api/Clubs/{request.ClubId}/OneTimeBadges/{b.Id}/image"
                        : null,
                })
                .ToListAsync(cancellationToken);

            var badgeIds = badges.Select(b => b.Id).ToList();

            var awardees = await (
                from motb in context.MemberOneTimeBadges
                join m in context.ClubMembers on motb.MemberId equals m.Id
                join u in context.Users on m.UserId equals u.Id
                where badgeIds.Contains(motb.OneTimeBadgeId)
                select new
                {
                    motb.OneTimeBadgeId,
                    Awardee = new OneTimeBadgeAwardeeDto
                    {
                        MemberOneTimeBadgeId = motb.Id,
                        MemberId = motb.MemberId,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        AwardedAt = motb.AwardedAt
                    }
                }
            ).ToListAsync(cancellationToken);

            var awardeesByBadge = awardees
                .GroupBy(a => a.OneTimeBadgeId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.Awardee).ToList());

            var result = badges.Select(b => new OneTimeBadgeDto
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                ImageUrl = b.ImageUrl,
                Awardees = awardeesByBadge.GetValueOrDefault(b.Id, [])
            }).ToList();

            return new GetOneTimeBadgesResult { Badges = result };
        }
    }
}
