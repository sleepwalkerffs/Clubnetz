using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetTrophyCase(int RequestingUserId, int TargetMemberId) : IQuery<GetTrophyCaseResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetTrophyCase, GetTrophyCaseResult>
    {
        public async Task<GetTrophyCaseResult> Handle(GetTrophyCase request, CancellationToken cancellationToken)
        {
            var targetMember = await context.Set<Member>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == request.TargetMemberId, cancellationToken);

            if (targetMember is null)
                return new GetTrophyCaseResult { IsOwnProfile = false, IsPublic = false, Clubs = [], ProfilePictureUrl = null, DisplayBadgeImageUrl = null, DisplayBadgeLevel = null };

            var isOwnProfile = targetMember.UserId == request.RequestingUserId;

            var settings = await context.MemberBadgeSettings
                .FirstOrDefaultAsync(s => s.MemberId == request.TargetMemberId, cancellationToken);

            var isPublic = settings?.TrophyCasePublic ?? true;

            var hasProfilePicture = await context.UserProfilePictures
                .AnyAsync(p => p.UserId == targetMember.UserId, cancellationToken);
            var profilePictureUrl = hasProfilePicture ? $"/api/Profile/picture/{targetMember.UserId}" : null;

            if (!isOwnProfile && !isPublic)
                return new GetTrophyCaseResult { IsOwnProfile = false, IsPublic = false, Clubs = [], ProfilePictureUrl = profilePictureUrl, DisplayBadgeImageUrl = null, DisplayBadgeLevel = null };

            // Get all member IDs for the target user (across clubs)
            var userMemberIds = await context.Set<Member>()
                .IgnoreQueryFilters()
                .Where(m => m.UserId == targetMember.UserId && m.MemberType == MemberType.ClubMember)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            var badges = await (
                from mb in context.MemberBadges
                join bt in context.BadgeTiers on mb.BadgeTierId equals bt.Id
                join s in context.Seasons on mb.SeasonId equals s.Id
                join c in context.Clubs.IgnoreQueryFilters() on bt.ClubId equals c.Id
                where userMemberIds.Contains(mb.MemberId)
                orderby c.Name, s.Period.From descending, bt.Level
                select new
                {
                    ClubId = c.Id,
                    ClubName = c.Name,
                    SeasonId = s.Id,
                    SeasonFrom = s.Period.From,
                    SeasonTo = s.Period.To,
                    Badge = new EarnedBadgeDto
                    {
                        MemberBadgeId = mb.Id,
                        BadgeTierId = bt.Id,
                        Level = bt.Level,
                        Name = bt.Name,
                        Description = bt.Description,
                        ImageUrl = context.BadgeTierImages.Any(i => i.BadgeTierId == bt.Id)
                            ? $"/api/Clubs/{bt.ClubId}/BadgeTiers/{bt.Id}/image"
                            : null,
                        EarnedAt = mb.EarnedAt,
                        SeasonId = s.Id,
                        SeasonLabel = $"{s.Period.From:dd.MM.yyyy} - {s.Period.To:dd.MM.yyyy}",
                        SeasonFrom = s.Period.From
                    }
                }
            ).ToListAsync(cancellationToken);

            var oneTimeBadgeAwards = await (
                from motb in context.MemberOneTimeBadges
                join otb in context.OneTimeBadges on motb.OneTimeBadgeId equals otb.Id
                join s in context.Seasons on otb.SeasonId equals s.Id
                join c in context.Clubs.IgnoreQueryFilters() on otb.ClubId equals c.Id
                where userMemberIds.Contains(motb.MemberId)
                orderby c.Name, s.Period.From descending, otb.Name
                select new
                {
                    ClubId = c.Id,
                    ClubName = c.Name,
                    SeasonId = s.Id,
                    SeasonFrom = s.Period.From,
                    SeasonTo = s.Period.To,
                    Badge = new OneTimeBadgeAwardDto
                    {
                        MemberOneTimeBadgeId = motb.Id,
                        OneTimeBadgeId = otb.Id,
                        Name = otb.Name,
                        Description = otb.Description,
                        ImageUrl = context.OneTimeBadgeImages.Any(i => i.OneTimeBadgeId == otb.Id)
                            ? $"/api/Clubs/{otb.ClubId}/OneTimeBadges/{otb.Id}/image"
                            : null,
                        AwardedAt = motb.AwardedAt,
                        SeasonId = s.Id,
                        SeasonLabel = $"{s.Period.From:dd.MM.yyyy} - {s.Period.To:dd.MM.yyyy}",
                        SeasonFrom = s.Period.From
                    }
                }
            ).ToListAsync(cancellationToken);

            var clubKeys = badges.Select(b => new { b.ClubId, b.ClubName })
                .Concat(oneTimeBadgeAwards.Select(b => new { b.ClubId, b.ClubName }))
                .Distinct();

            var clubs = clubKeys
                .Select(clubKey =>
                {
                    var clubBadges = badges.Where(b => b.ClubId == clubKey.ClubId);
                    var clubOneTimeBadges = oneTimeBadgeAwards.Where(b => b.ClubId == clubKey.ClubId);

                    var seasonKeys = clubBadges.Select(b => new { b.SeasonId, b.SeasonFrom, b.SeasonTo })
                        .Concat(clubOneTimeBadges.Select(b => new { b.SeasonId, b.SeasonFrom, b.SeasonTo }))
                        .Distinct()
                        .OrderByDescending(s => s.SeasonFrom);

                    return new ClubTrophiesDto
                    {
                        ClubId = clubKey.ClubId,
                        ClubName = clubKey.ClubName,
                        Seasons = seasonKeys.Select(seasonKey => new SeasonTrophiesDto
                        {
                            SeasonId = seasonKey.SeasonId,
                            SeasonFrom = seasonKey.SeasonFrom,
                            SeasonLabel = $"{seasonKey.SeasonFrom:dd.MM.yyyy} - {seasonKey.SeasonTo:dd.MM.yyyy}",
                            Badges = clubBadges.Where(b => b.SeasonId == seasonKey.SeasonId).Select(b => b.Badge).ToList(),
                            OneTimeBadges = clubOneTimeBadges.Where(b => b.SeasonId == seasonKey.SeasonId).Select(b => b.Badge).ToList()
                        }).ToList()
                    };
                })
                .ToList();

            var displayBadge = await (
                from s2 in context.MemberBadgeSettings
                join mb2 in context.MemberBadges on s2.DisplayBadgeId equals mb2.Id
                join bt2 in context.BadgeTiers on mb2.BadgeTierId equals bt2.Id
                where s2.MemberId == request.TargetMemberId
                select new { TierId = bt2.Id, bt2.ClubId, bt2.Level, HasImage = context.BadgeTierImages.Any(i => i.BadgeTierId == bt2.Id) }
            ).FirstOrDefaultAsync(cancellationToken);

            var displayOneTimeBadge = await (
                from s2 in context.MemberBadgeSettings
                join motb2 in context.MemberOneTimeBadges on s2.DisplayOneTimeBadgeId equals motb2.Id
                join otb2 in context.OneTimeBadges on motb2.OneTimeBadgeId equals otb2.Id
                where s2.MemberId == request.TargetMemberId
                select new { BadgeId = otb2.Id, otb2.ClubId, HasImage = context.OneTimeBadgeImages.Any(i => i.OneTimeBadgeId == otb2.Id) }
            ).FirstOrDefaultAsync(cancellationToken);

            string? displayBadgeImageUrl;
            int? displayBadgeLevel;
            if (displayBadge is not null)
            {
                displayBadgeImageUrl = displayBadge.HasImage ? $"/api/Clubs/{displayBadge.ClubId}/BadgeTiers/{displayBadge.TierId}/image" : null;
                displayBadgeLevel = displayBadge.Level;
            }
            else if (displayOneTimeBadge is not null)
            {
                displayBadgeImageUrl = displayOneTimeBadge.HasImage ? $"/api/Clubs/{displayOneTimeBadge.ClubId}/OneTimeBadges/{displayOneTimeBadge.BadgeId}/image" : null;
                displayBadgeLevel = null;
            }
            else
            {
                displayBadgeImageUrl = null;
                displayBadgeLevel = null;
            }

            return new GetTrophyCaseResult
            {
                IsOwnProfile = isOwnProfile,
                IsPublic = isPublic,
                ProfilePictureUrl = profilePictureUrl,
                DisplayBadgeImageUrl = displayBadgeImageUrl,
                DisplayBadgeLevel = displayBadgeLevel,
                Clubs = clubs
            };
        }
    }
}
