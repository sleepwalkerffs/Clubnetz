using Bookennis.Api.Data;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetMemberBadgeProgress(int MemberId, int ClubId) : IQuery<GetMemberBadgeProgressResult>
{
    public class Handler(AppDbContext context, IBadgeProgressionService badgeProgressionService) : IRequestHandler<GetMemberBadgeProgress, GetMemberBadgeProgressResult>
    {
        public async Task<GetMemberBadgeProgressResult> Handle(GetMemberBadgeProgress request, CancellationToken cancellationToken)
        {
            // Find the active season for this club
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = await context.Seasons
                .Where(s => s.ClubId == request.ClubId)
                .Where(s => s.Period.From <= today && s.Period.To >= today)
                .FirstOrDefaultAsync(cancellationToken);

            if (season is null)
            {
                return new GetMemberBadgeProgressResult
                {
                    MatchCount = 0,
                    CurrentBadgeTierId = null,
                    CurrentBadgeName = null,
                    CurrentBadgeLevel = null,
                    CurrentBadgeImageUrl = null,
                    NextBadgeTierId = null,
                    NextBadgeName = null,
                    NextBadgeMatchesRequired = null,
                    NextBadgeLevel = null,
                    NextBadgeImageUrl = null,
                    EarnedBadges = [],
                    OneTimeBadges = [],
                    DisplayBadgeId = null,
                    DisplayOneTimeBadgeId = null,
                    TrophyCasePublic = true
                };
            }

            // Trigger lazy badge awarding
            await badgeProgressionService.CheckAndAwardBadges(request.MemberId, request.ClubId, season.Id, cancellationToken);

            var chargingPlayModeIds = await context.PlayModes
                .Where(pm => pm.IsChargingBookingSubscription)
                .Select(pm => pm.Id)
                .ToListAsync(cancellationToken);

            var matchCount = 0;
            if (chargingPlayModeIds.Count > 0)
            {
                matchCount = await context.BookingPlayers
                    .Where(bp => bp.MemberId == request.MemberId)
                    .Where(bp => chargingPlayModeIds.Contains(bp.Booking.PlayModeId))
                    .Where(bp => bp.Booking.Interval.From >= season.Period.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
                    .Where(bp => bp.Booking.Interval.To <= season.Period.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc))
                    .Where(bp => bp.Booking.Interval.To < DateTimeOffset.UtcNow)
                    .CountAsync(cancellationToken);
            }

            var badgeTiers = await context.BadgeTiers
                .Where(bt => bt.ClubId == request.ClubId && bt.SeasonId == season.Id)
                .OrderBy(bt => bt.Level)
                .ToListAsync(cancellationToken);

            var earnedBadges = await (
                from mb in context.MemberBadges
                join bt in context.BadgeTiers on mb.BadgeTierId equals bt.Id
                where mb.MemberId == request.MemberId && mb.SeasonId == season.Id
                orderby bt.Level
                select new EarnedBadgeDto
                {
                    MemberBadgeId = mb.Id,
                    BadgeTierId = bt.Id,
                    Level = bt.Level,
                    Name = bt.Name,
                    Description = bt.Description,
                    ImageUrl = context.BadgeTierImages.Any(i => i.BadgeTierId == bt.Id)
                        ? $"/api/Clubs/{request.ClubId}/BadgeTiers/{bt.Id}/image"
                        : null,
                    EarnedAt = mb.EarnedAt,
                    SeasonId = season.Id,
                    SeasonLabel = $"{season.Period.From:dd.MM.yyyy} - {season.Period.To:dd.MM.yyyy}",
                    SeasonFrom = season.Period.From
                }
            ).ToListAsync(cancellationToken);

            var oneTimeBadges = await (
                from motb in context.MemberOneTimeBadges
                join otb in context.OneTimeBadges on motb.OneTimeBadgeId equals otb.Id
                where motb.MemberId == request.MemberId && otb.SeasonId == season.Id
                orderby otb.Name
                select new OneTimeBadgeAwardDto
                {
                    MemberOneTimeBadgeId = motb.Id,
                    OneTimeBadgeId = otb.Id,
                    Name = otb.Name,
                    Description = otb.Description,
                    ImageUrl = context.OneTimeBadgeImages.Any(i => i.OneTimeBadgeId == otb.Id)
                        ? $"/api/Clubs/{request.ClubId}/OneTimeBadges/{otb.Id}/image"
                        : null,
                    AwardedAt = motb.AwardedAt,
                    SeasonId = season.Id,
                    SeasonLabel = $"{season.Period.From:dd.MM.yyyy} - {season.Period.To:dd.MM.yyyy}",
                    SeasonFrom = season.Period.From
                }
            ).ToListAsync(cancellationToken);

            var currentBadge = badgeTiers.LastOrDefault(t => matchCount >= t.MatchesRequired);
            var nextBadge = badgeTiers.FirstOrDefault(t => matchCount < t.MatchesRequired);

            var badgeSettings = await context.MemberBadgeSettings
                .FirstOrDefaultAsync(s => s.MemberId == request.MemberId, cancellationToken);

            return new GetMemberBadgeProgressResult
            {
                MatchCount = matchCount,
                CurrentBadgeTierId = currentBadge?.Id,
                CurrentBadgeName = currentBadge?.Name,
                CurrentBadgeLevel = currentBadge?.Level,
                CurrentBadgeImageUrl = currentBadge is not null && await context.BadgeTierImages.AnyAsync(i => i.BadgeTierId == currentBadge.Id, cancellationToken)
                    ? $"/api/Clubs/{request.ClubId}/BadgeTiers/{currentBadge.Id}/image"
                    : null,
                NextBadgeTierId = nextBadge?.Id,
                NextBadgeName = nextBadge?.Name,
                NextBadgeMatchesRequired = nextBadge?.MatchesRequired,
                NextBadgeLevel = nextBadge?.Level,
                NextBadgeImageUrl = nextBadge is not null && await context.BadgeTierImages.AnyAsync(i => i.BadgeTierId == nextBadge.Id, cancellationToken)
                    ? $"/api/Clubs/{request.ClubId}/BadgeTiers/{nextBadge.Id}/image"
                    : null,
                EarnedBadges = earnedBadges,
                OneTimeBadges = oneTimeBadges,
                DisplayBadgeId = badgeSettings?.DisplayBadgeId,
                DisplayOneTimeBadgeId = badgeSettings?.DisplayOneTimeBadgeId,
                TrophyCasePublic = badgeSettings?.TrophyCasePublic ?? true
            };
        }
    }
}
