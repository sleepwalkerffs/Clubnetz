using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.Shared;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Api.Infrastructure;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetMemberBadgeProgress(int MemberId, int ClubId) : IQuery<GetMemberBadgeProgressResult>
{
    public class Handler(AppDbContext context, IBadgeProgressionService badgeProgressionService, IUserLanguageAccessor languageAccessor) : IRequestHandler<GetMemberBadgeProgress, GetMemberBadgeProgressResult>
    {
        public async Task<GetMemberBadgeProgressResult> Handle(GetMemberBadgeProgress request, CancellationToken cancellationToken)
        {
            // Find the active season for this club (today is the local day of the club)
            var region = await context.GetClubRegion(request.ClubId, cancellationToken);
            var culture = region.Culture(languageAccessor.GetCultureInfo());
            var today = region.Today();
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

            var badgeTiers = await context.BadgeTiers
                .Where(bt => bt.ClubId == request.ClubId && bt.SeasonId == season.Id)
                .OrderBy(bt => bt.Level)
                .ToListAsync(cancellationToken);

            var tierIdsWithImage = await context.BadgeTierImages
                .Where(i => badgeTiers.Select(t => t.Id).Contains(i.BadgeTierId))
                .Select(i => i.BadgeTierId)
                .ToListAsync(cancellationToken);

            string? ImageUrl(BadgeTier? tier) => tier is not null && tierIdsWithImage.Contains(tier.Id)
                ? $"/api/Clubs/{request.ClubId}/BadgeTiers/{tier.Id}/image"
                : null;

            // One track per sport that has tiers (null = every sport); without tiers the matches of every sport are shown
            var trackSports = badgeTiers
                .Select(t => t.Sport)
                .Distinct()
                .OrderBy(sport => sport.HasValue)
                .ThenBy(sport => sport)
                .ToList();
            if (trackSports.Count == 0)
                trackSports.Add(null);

            var matchCounts = await BadgeMatches.CountPerTrack(context, request.MemberId, request.ClubId, season, trackSports, cancellationToken);

            var tracks = trackSports
                .Select(sport =>
                {
                    var tiers = badgeTiers.Where(t => t.Sport == sport).ToList();
                    var matchCount = matchCounts[BadgeMatches.Track(sport)];
                    var current = tiers.LastOrDefault(t => matchCount >= t.MatchesRequired);
                    var next = tiers.FirstOrDefault(t => matchCount < t.MatchesRequired);
                    return new BadgeTrackProgressDto
                    {
                        Sport = sport.ToDto(),
                        MatchCount = matchCount,
                        CurrentBadgeTierId = current?.Id,
                        CurrentBadgeName = current?.Name,
                        CurrentBadgeLevel = current?.Level,
                        CurrentBadgeImageUrl = ImageUrl(current),
                        NextBadgeTierId = next?.Id,
                        NextBadgeName = next?.Name,
                        NextBadgeMatchesRequired = next?.MatchesRequired,
                        NextBadgeLevel = next?.Level,
                        NextBadgeImageUrl = ImageUrl(next),
                    };
                })
                .ToList();

            var earnedBadges = await (
                from mb in context.MemberBadges
                join bt in context.BadgeTiers on mb.BadgeTierId equals bt.Id
                where mb.MemberId == request.MemberId && mb.SeasonId == season.Id
                orderby bt.Sport.HasValue, bt.Sport, bt.Level
                select new EarnedBadgeDto
                {
                    MemberBadgeId = mb.Id,
                    BadgeTierId = bt.Id,
                    Level = bt.Level,
                    Sport = (Bookennis.Shared.Controller.Shared.Sport?)bt.Sport,
                    Name = bt.Name,
                    Description = bt.Description,
                    ImageUrl = context.BadgeTierImages.Any(i => i.BadgeTierId == bt.Id)
                        ? $"/api/Clubs/{request.ClubId}/BadgeTiers/{bt.Id}/image"
                        : null,
                    EarnedAt = mb.EarnedAt,
                    SeasonId = season.Id,
                    SeasonLabel = BadgeSeasonLabel.Of(season.Period.From, season.Period.To, culture),
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
                    SeasonLabel = BadgeSeasonLabel.Of(season.Period.From, season.Period.To, culture),
                    SeasonFrom = season.Period.From
                }
            ).ToListAsync(cancellationToken);

            // The fields of the result itself are the first track: every sport, or the first sport with tiers
            var primary = tracks[0];

            var badgeSettings = await context.MemberBadgeSettings
                .FirstOrDefaultAsync(s => s.MemberId == request.MemberId, cancellationToken);

            return new GetMemberBadgeProgressResult
            {
                MatchCount = primary.MatchCount,
                CurrentBadgeTierId = primary.CurrentBadgeTierId,
                CurrentBadgeName = primary.CurrentBadgeName,
                CurrentBadgeLevel = primary.CurrentBadgeLevel,
                CurrentBadgeImageUrl = primary.CurrentBadgeImageUrl,
                NextBadgeTierId = primary.NextBadgeTierId,
                NextBadgeName = primary.NextBadgeName,
                NextBadgeMatchesRequired = primary.NextBadgeMatchesRequired,
                NextBadgeLevel = primary.NextBadgeLevel,
                NextBadgeImageUrl = primary.NextBadgeImageUrl,
                Tracks = tracks,
                EarnedBadges = earnedBadges,
                OneTimeBadges = oneTimeBadges,
                DisplayBadgeId = badgeSettings?.DisplayBadgeId,
                DisplayOneTimeBadgeId = badgeSettings?.DisplayOneTimeBadgeId,
                TrophyCasePublic = badgeSettings?.TrophyCasePublic ?? true
            };
        }
    }
}
