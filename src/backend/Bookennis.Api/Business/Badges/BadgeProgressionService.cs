using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.Shared;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
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
        var season = await context.Seasons
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (season is null)
            return;

        var badgeTiers = await context.BadgeTiers
            .Where(bt => bt.ClubId == clubId && bt.SeasonId == seasonId)
            .OrderBy(bt => bt.Level)
            .ToListAsync(cancellationToken);

        if (badgeTiers.Count == 0)
            return;

        var alreadyEarned = await context.MemberBadges
            .Where(mb => mb.MemberId == memberId && mb.SeasonId == seasonId)
            .Select(mb => mb.BadgeTierId)
            .ToListAsync(cancellationToken);

        // Every track (a sport, or every sport) counts its own matches
        var matchCounts = await BadgeMatches.CountPerTrack(context, memberId, clubId, season, badgeTiers.Select(t => t.Sport), cancellationToken);

        foreach (var tier in badgeTiers)
        {
            if (matchCounts[BadgeMatches.Track(tier.Sport)] >= tier.MatchesRequired && !alreadyEarned.Contains(tier.Id))
            {
                context.MemberBadges.Add(new MemberBadge(memberId, tier.Id, seasonId));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// The matches that count for the tier badges of a season: played bookings in play modes that charge the booking subscription,
/// within the local days of the season. A tier with a sport only counts the bookings on courts of that sport.
/// </summary>
public static class BadgeMatches
{
    /// <summary>Dictionary key of a track (a dictionary can't have a <c>null</c> key): -1 = every sport.</summary>
    public static int Track(Sport? sport) => sport is { } s ? (int)s : -1;

    /// <summary>The member's matches per track, keyed with <see cref="Track"/>.</summary>
    public static async Task<Dictionary<int, int>> CountPerTrack(AppDbContext context, int memberId, int clubId, Season season, IEnumerable<Sport?> sports, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<int, int>();
        foreach (var sport in sports.Distinct())
            counts[Track(sport)] = await Count(context, memberId, clubId, season, sport, cancellationToken);

        return counts;
    }

    public static async Task<int> Count(AppDbContext context, int memberId, int clubId, Season season, Sport? sport, CancellationToken cancellationToken)
    {
        var chargingPlayModeIds = await context.PlayModes
            .Where(pm => pm.IsChargingBookingSubscription)
            .Select(pm => pm.Id)
            .ToListAsync(cancellationToken);

        if (chargingPlayModeIds.Count == 0)
            return 0;

        // The season from the start of its first to the end of its last local day of the club
        var region = await context.GetClubRegion(clubId, cancellationToken);
        var seasonStart = region.StartOfDay(season.Period.From).ToUniversalTime();
        var seasonEnd = region.StartOfDay(season.Period.To.AddDays(1)).ToUniversalTime();

        return await context.BookingPlayers
            .OfSport(context, sport)
            .Where(bp => bp.MemberId == memberId)
            .Where(bp => chargingPlayModeIds.Contains(bp.Booking.PlayModeId))
            .Where(bp => bp.Booking.Interval.From >= seasonStart)
            .Where(bp => bp.Booking.Interval.To <= seasonEnd)
            .Where(bp => bp.Booking.Interval.To < DateTimeOffset.UtcNow)
            .CountAsync(cancellationToken);
    }
}
