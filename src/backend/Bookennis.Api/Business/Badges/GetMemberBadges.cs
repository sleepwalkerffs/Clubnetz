using Bookennis.Api.Data;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetMemberBadges(int MemberId, int ClubId, int? SeasonId) : IQuery<GetMemberBadgesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMemberBadges, GetMemberBadgesResult>
    {
        public async Task<GetMemberBadgesResult> Handle(GetMemberBadges request, CancellationToken cancellationToken)
        {
            var query = from mb in context.MemberBadges
                        join bt in context.BadgeTiers on mb.BadgeTierId equals bt.Id
                        join s in context.Seasons on mb.SeasonId equals s.Id
                        where mb.MemberId == request.MemberId && bt.ClubId == request.ClubId
                        orderby s.Period.From descending, bt.Level
                        select new { mb, bt, s };

            if (request.SeasonId.HasValue)
                query = query.Where(x => x.mb.SeasonId == request.SeasonId.Value);

            var badges = await query.Select(x => new EarnedBadgeDto
            {
                MemberBadgeId = x.mb.Id,
                BadgeTierId = x.bt.Id,
                Level = x.bt.Level,
                Name = x.bt.Name,
                Description = x.bt.Description,
                ImageUrl = context.BadgeTierImages.Any(i => i.BadgeTierId == x.bt.Id)
                    ? $"/api/Clubs/{request.ClubId}/BadgeTiers/{x.bt.Id}/image"
                    : null,
                EarnedAt = x.mb.EarnedAt,
                SeasonId = x.s.Id,
                SeasonLabel = $"{x.s.Period.From:dd.MM.yyyy} - {x.s.Period.To:dd.MM.yyyy}",
                SeasonFrom = x.s.Period.From
            }).ToListAsync(cancellationToken);

            var oneTimeQuery = from motb in context.MemberOneTimeBadges
                                join otb in context.OneTimeBadges on motb.OneTimeBadgeId equals otb.Id
                                join s in context.Seasons on otb.SeasonId equals s.Id
                                where motb.MemberId == request.MemberId && otb.ClubId == request.ClubId
                                orderby s.Period.From descending, otb.Name
                                select new { motb, otb, s };

            if (request.SeasonId.HasValue)
                oneTimeQuery = oneTimeQuery.Where(x => x.otb.SeasonId == request.SeasonId.Value);

            var oneTimeBadges = await oneTimeQuery.Select(x => new OneTimeBadgeAwardDto
            {
                MemberOneTimeBadgeId = x.motb.Id,
                OneTimeBadgeId = x.otb.Id,
                Name = x.otb.Name,
                Description = x.otb.Description,
                ImageUrl = context.OneTimeBadgeImages.Any(i => i.OneTimeBadgeId == x.otb.Id)
                    ? $"/api/Clubs/{request.ClubId}/OneTimeBadges/{x.otb.Id}/image"
                    : null,
                AwardedAt = x.motb.AwardedAt,
                SeasonId = x.s.Id,
                SeasonLabel = $"{x.s.Period.From:dd.MM.yyyy} - {x.s.Period.To:dd.MM.yyyy}",
                SeasonFrom = x.s.Period.From
            }).ToListAsync(cancellationToken);

            return new GetMemberBadgesResult { Badges = badges, OneTimeBadges = oneTimeBadges };
        }
    }
}
