using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public interface IBadgeProgressionService
{
    public Task CheckAndAwardBadges(int memberId, int clubId, int seasonId, CancellationToken cancellationToken);
}

public class BadgeProgressionService(AppDbContext context) : IBadgeProgressionService
{
    public async Task CheckAndAwardBadges(int memberId, int clubId, int seasonId, CancellationToken cancellationToken)
    {
        var chargingPlayModeIds = await context.PlayModes
            .Where(pm => pm.IsChargingBookingSubscription)
            .Select(pm => pm.Id)
            .ToListAsync(cancellationToken);

        if (chargingPlayModeIds.Count == 0)
            return;

        var season = await context.Seasons
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (season is null)
            return;

        var matchCount = await context.BookingPlayers
            .Where(bp => bp.MemberId == memberId)
            .Where(bp => chargingPlayModeIds.Contains(bp.Booking.PlayModeId))
            .Where(bp => bp.Booking.Interval.From >= season.Period.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Where(bp => bp.Booking.Interval.To <= season.Period.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc))
            .Where(bp => bp.Booking.Interval.To < DateTimeOffset.UtcNow)
            .CountAsync(cancellationToken);

        var badgeTiers = await context.BadgeTiers
            .Where(bt => bt.ClubId == clubId && bt.SeasonId == seasonId)
            .OrderBy(bt => bt.Level)
            .ToListAsync(cancellationToken);

        var alreadyEarned = await context.MemberBadges
            .Where(mb => mb.MemberId == memberId && mb.SeasonId == seasonId)
            .Select(mb => mb.BadgeTierId)
            .ToListAsync(cancellationToken);

        foreach (var tier in badgeTiers)
        {
            if (matchCount >= tier.MatchesRequired && !alreadyEarned.Contains(tier.Id))
            {
                context.MemberBadges.Add(new MemberBadge(memberId, tier.Id, seasonId));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
